#if ANDROID
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AmoraApp.Config;
using Microsoft.Maui.ApplicationModel; // Platform.CurrentActivity
using Android.BillingClient.Api;

namespace AmoraApp.Services
{
    public enum IapProductKind
    {
        Subscription,
        InApp
    }

    public sealed class GooglePlayBillingService : Java.Lang.Object, IPurchasesUpdatedListener
    {
        public static GooglePlayBillingService Instance { get; } = new GooglePlayBillingService();

        private BillingClient? _client;
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

        private TaskCompletionSource<(bool ok, string message)>? _purchaseTcs;

        // ✅ ResponseCode agora é enum no seu binding
        private static readonly BillingResponseCode RC_OK = BillingResponseCode.Ok;
        private static readonly BillingResponseCode RC_USER_CANCELED = BillingResponseCode.UserCancelled;

        // skuType strings (oficiais)
        private const string SKU_SUBS = "subs";
        private const string SKU_INAPP = "inapp";

        private readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Realtime Database base URL (sem barra final)
        private string BaseUrl => (FirebaseSettings.DatabaseUrl ?? "").Trim().TrimEnd('/');

        private GooglePlayBillingService() { }

        // =========================================================
        // PUBLIC API
        // =========================================================

        public Task<(bool ok, string message)> BuySubscriptionAsync(
            string uid,
            string productId,
            Func<string, Task> grantEntitlementAsync)
        {
            return BuyAsync(uid, SKU_SUBS, productId, grantEntitlementAsync, consumeAfterGrant: false);
        }

        public Task<(bool ok, string message)> BuyInAppAsync(
            string uid,
            string productId,
            Func<string, Task> grantEntitlementAsync)
        {
            // Boosts consumíveis
            return BuyAsync(uid, SKU_INAPP, productId, grantEntitlementAsync, consumeAfterGrant: true);
        }

        public async Task RestorePurchasesAsync(string uid)
        {
            await EnsureConnectedAsync();
            await QueryAndProcessExistingPurchasesAsync(uid, SKU_SUBS);
            await QueryAndProcessExistingPurchasesAsync(uid, SKU_INAPP);
        }

        // =========================================================
        // BUY FLOW (SkuDetails)
        // =========================================================

        private async Task<(bool ok, string message)> BuyAsync(
            string uid,
            string skuType,
            string productId,
            Func<string, Task> grantEntitlementAsync,
            bool consumeAfterGrant)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return (false, "Login necessário.");

            if (string.IsNullOrWhiteSpace(productId))
                return (false, "Produto inválido.");

            await EnsureConnectedAsync();

            var sku = await GetSkuDetailsAsync(skuType, productId);
            if (sku == null)
                return (false, "Produto não encontrado no Google Play. Verifique se está ativo e se você está em teste interno.");

            var act = Platform.CurrentActivity;
            if (act == null)
                return (false, "Activity Android indisponível para abrir o Google Play.");

            _purchaseTcs = new TaskCompletionSource<(bool ok, string message)>();

            var flowParams = BillingFlowParams.NewBuilder()
                .SetSkuDetails(sku)
                .Build();

            var launch = LaunchBillingFlowCompat(_client!, act, flowParams);

            // launch é BillingResult, mas via reflection
            var rc = GetResponseCodeCompat(launch);
            if (rc != RC_OK)
                return (false, $"Falha ao iniciar pagamento: {GetDebugMessageCompat(launch) ?? "erro"}");

            // Resultado real chega no callback OnPurchasesUpdated
            return await _purchaseTcs.Task;
        }

        private async Task<SkuDetails?> GetSkuDetailsAsync(string skuType, string productId)
        {
            await EnsureConnectedAsync();

            var p = SkuDetailsParams.NewBuilder()
                .SetSkusList(new List<string> { productId })
                .SetType(skuType)
                .Build();

            // Tentativa A: QuerySkuDetailsAsync(SkuDetailsParams, ISkuDetailsResponseListener)
            var mi2 = _client!.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "QuerySkuDetailsAsync" && m.GetParameters().Length == 2);

            if (mi2 != null)
            {
                var tcs = new TaskCompletionSource<SkuDetails?>();

                var listener = new SkuDetailsResponseListener((result, list) =>
                {
                    try
                    {
                        if (result == null || result.ResponseCode != RC_OK || list == null)
                        {
                            tcs.TrySetResult(null);
                            return;
                        }

                        var found = list.FirstOrDefault(x => string.Equals(x?.Sku, productId, StringComparison.Ordinal));
                        tcs.TrySetResult(found);
                    }
                    catch
                    {
                        tcs.TrySetResult(null);
                    }
                });

                mi2.Invoke(_client, new object[] { p, listener });
                return await tcs.Task;
            }

            // Tentativa B: QuerySkuDetailsAsync(SkuDetailsParams) -> result object
            var mi1 = _client.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "QuerySkuDetailsAsync" && m.GetParameters().Length == 1);

            if (mi1 != null)
            {
                var ret = mi1.Invoke(_client, new object[] { p });
                return ExtractSkuFromSkuDetailsResult(ret, productId);
            }

            // Tentativa C: QuerySkuDetails(SkuDetailsParams) sync
            var miSync = _client.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "QuerySkuDetails" && m.GetParameters().Length == 1);

            if (miSync != null)
            {
                var ret = miSync.Invoke(_client, new object[] { p });
                return ExtractSkuFromSkuDetailsResult(ret, productId);
            }

            return null;
        }

        private sealed class SkuDetailsResponseListener : Java.Lang.Object, ISkuDetailsResponseListener
        {
            private readonly Action<BillingResult?, IList<SkuDetails>?> _cb;
            public SkuDetailsResponseListener(Action<BillingResult?, IList<SkuDetails>?> cb) => _cb = cb;
            public void OnSkuDetailsResponse(BillingResult? p0, IList<SkuDetails>? p1) => _cb(p0, p1);
        }

        private static SkuDetails? ExtractSkuFromSkuDetailsResult(object? resultObj, string productId)
        {
            if (resultObj == null) return null;

            // propriedades típicas: SkuDetailsList, SkusDetailsList, List
            var prop = resultObj.GetType().GetProperty("SkuDetailsList", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(resultObj) is IList<SkuDetails> list1)
                return list1.FirstOrDefault(x => string.Equals(x?.Sku, productId, StringComparison.Ordinal));

            prop = resultObj.GetType().GetProperty("SkusDetailsList", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(resultObj) is IList<SkuDetails> list2)
                return list2.FirstOrDefault(x => string.Equals(x?.Sku, productId, StringComparison.Ordinal));

            prop = resultObj.GetType().GetProperty("List", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(resultObj) is IList<SkuDetails> list3)
                return list3.FirstOrDefault(x => string.Equals(x?.Sku, productId, StringComparison.Ordinal));

            return null;
        }

        // =========================================================
        // PURCHASE CALLBACK
        // =========================================================

        public void OnPurchasesUpdated(BillingResult? billingResult, IList<Purchase>? purchases)
        {
            if (_purchaseTcs == null)
                return;

            if (billingResult == null)
            {
                _purchaseTcs.TrySetResult((false, "Resposta inválida do Google Play."));
                return;
            }

            // ✅ agora compara enum com enum
            if (billingResult.ResponseCode == RC_USER_CANCELED)
            {
                _purchaseTcs.TrySetResult((false, "Compra cancelada."));
                return;
            }

            if (billingResult.ResponseCode != RC_OK)
            {
                _purchaseTcs.TrySetResult((false, $"Erro na compra: {billingResult.DebugMessage}"));
                return;
            }

            if (purchases == null || purchases.Count == 0)
            {
                _purchaseTcs.TrySetResult((false, "Nenhuma compra retornada."));
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var purchase = purchases[0];
                    var ok = await ProcessPurchaseAsync(purchase);

                    _purchaseTcs?.TrySetResult(ok
                        ? (true, "Compra concluída com sucesso.")
                        : (false, "Não foi possível processar a compra."));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                    _purchaseTcs?.TrySetResult((false, "Erro ao finalizar compra."));
                }
            });
        }

        // =========================================================
        // CONNECT
        // =========================================================

        private async Task EnsureConnectedAsync()
        {
            await _connectLock.WaitAsync();
            try
            {
                if (_client != null && _client.IsReady)
                    return;

                _client?.EndConnection();

                _client = BillingClient.NewBuilder(Android.App.Application.Context)
                    .EnablePendingPurchases()
                    .SetListener(this)
                    .Build();

                var tcs = new TaskCompletionSource<bool>();
                _client.StartConnection(new BillingConnectionListener(tcs));

                var ok = await tcs.Task;
                if (!ok || _client == null || !_client.IsReady)
                    throw new Exception("Falha ao conectar no Google Play Billing.");
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private sealed class BillingConnectionListener : Java.Lang.Object, IBillingClientStateListener
        {
            private readonly TaskCompletionSource<bool> _tcs;
            public BillingConnectionListener(TaskCompletionSource<bool> tcs) => _tcs = tcs;

            public void OnBillingSetupFinished(BillingResult? result)
            {
                var ok = result != null && result.ResponseCode == RC_OK;
                _tcs.TrySetResult(ok);
            }

            public void OnBillingServiceDisconnected()
            {
                _tcs.TrySetResult(false);
            }
        }

        // =========================================================
        // PROCESS PURCHASE: idempotência + ack/consume + grant
        // =========================================================

        private async Task<bool> ProcessPurchaseAsync(Purchase purchase)
        {
            // Estado Purchased (enum separado)
            if (purchase.PurchaseState != PurchaseState.Purchased)
                return false;

            var uid = FirebaseAuthService.Instance.CurrentUserUid ?? "";
            if (string.IsNullOrWhiteSpace(uid))
                return false;

            var token = purchase.PurchaseToken ?? "";
            if (string.IsNullOrWhiteSpace(token))
                return false;

            // Idempotência: evita duplicar grants
            if (await IsTokenAlreadyGrantedAsync(uid, token))
                return true;

            // Ack se necessário
            if (!purchase.IsAcknowledged)
            {
                var ackOk = await AcknowledgeAsync(token);
                if (!ackOk) return false;
            }

            // SKU / productId (COMPAT: Products / Skus)
            var productId = GetPurchasedProductIdCompat(purchase);
            if (string.IsNullOrWhiteSpace(productId))
                return false;

            // Concede benefício (Plus/Premium/Boost)
            await GrantByProductIdAsync(uid, productId);

            // Marca token como concedido
            await MarkTokenGrantedAsync(uid, token, productId, purchase.OrderId ?? "");

            // Consumir boosts (consumível) para permitir compra repetida
            if (IsBoostProduct(productId))
            {
                await ConsumeAsync(token);
            }

            return true;
        }

        private static string GetPurchasedProductIdCompat(Purchase purchase)
        {
            // Billing v6+: Products (IList<string>)
            try
            {
                var pProducts = purchase.GetType().GetProperty("Products", BindingFlags.Public | BindingFlags.Instance);
                if (pProducts?.GetValue(purchase) is IList<string> products && products.Count > 0)
                    return products[0];
            }
            catch { }

            // Billing antigo: Skus (IList<string>)
            try
            {
                var pSkus = purchase.GetType().GetProperty("Skus", BindingFlags.Public | BindingFlags.Instance);
                if (pSkus?.GetValue(purchase) is IList<string> skus && skus.Count > 0)
                    return skus[0];
            }
            catch { }

            return "";
        }

        private bool IsBoostProduct(string productId)
            => productId == "boost_3" || productId == "boost_10";

        private async Task GrantByProductIdAsync(string uid, string productId)
        {
            // Assinaturas
            if (productId == "plus_monthly")
                await PlanService.Instance.ActivatePlanAsync(uid, PlanType.Plus, PlanPeriod.Monthly);
            else if (productId == "plus_yearly")
                await PlanService.Instance.ActivatePlanAsync(uid, PlanType.Plus, PlanPeriod.Yearly);
            else if (productId == "premium_monthly")
                await PlanService.Instance.ActivatePlanAsync(uid, PlanType.Premium, PlanPeriod.Monthly);
            else if (productId == "premium_yearly")
                await PlanService.Instance.ActivatePlanAsync(uid, PlanType.Premium, PlanPeriod.Yearly);

            // Boosts
            else if (productId == "boost_3")
                await PlanService.Instance.AddUserBoostsAsync(uid, 3);
            else if (productId == "boost_10")
                await PlanService.Instance.AddUserBoostsAsync(uid, 10);
        }

        private async Task<bool> AcknowledgeAsync(string purchaseToken)
        {
            await EnsureConnectedAsync();

            var tcs = new TaskCompletionSource<bool>();

            var p = AcknowledgePurchaseParams.NewBuilder()
                .SetPurchaseToken(purchaseToken)
                .Build();

            AcknowledgePurchaseCompat(_client!, p, new AcknowledgeListener(result =>
            {
                tcs.TrySetResult(result != null && result.ResponseCode == RC_OK);
            }));

            return await tcs.Task;
        }

        private async Task<bool> ConsumeAsync(string purchaseToken)
        {
            await EnsureConnectedAsync();

            var tcs = new TaskCompletionSource<bool>();

            var p = ConsumeParams.NewBuilder()
                .SetPurchaseToken(purchaseToken)
                .Build();

            ConsumeAsyncCompat(_client!, p, new ConsumeListener((result, outToken) =>
            {
                tcs.TrySetResult(result != null && result.ResponseCode == RC_OK);
            }));

            return await tcs.Task;
        }

        private sealed class AcknowledgeListener : Java.Lang.Object, IAcknowledgePurchaseResponseListener
        {
            private readonly Action<BillingResult?> _cb;
            public AcknowledgeListener(Action<BillingResult?> cb) => _cb = cb;
            public void OnAcknowledgePurchaseResponse(BillingResult? p0) => _cb(p0);
        }

        private sealed class ConsumeListener : Java.Lang.Object, IConsumeResponseListener
        {
            private readonly Action<BillingResult?, string?> _cb;
            public ConsumeListener(Action<BillingResult?, string?> cb) => _cb = cb;
            public void OnConsumeResponse(BillingResult? p0, string? p1) => _cb(p0, p1);
        }

        // =========================================================
        // RESTORE / QUERY PURCHASES (compat)
        // =========================================================

        private async Task QueryAndProcessExistingPurchasesAsync(string uid, string skuType)
        {
            await EnsureConnectedAsync();

            var purchases = await QueryPurchasesCompatAsync(skuType);

            foreach (var pur in purchases)
            {
                try { await ProcessPurchaseAsync(pur); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
            }
        }

        private async Task<IList<Purchase>> QueryPurchasesCompatAsync(string skuType)
        {
            await EnsureConnectedAsync();

            // Tentativa A: QueryPurchasesAsync(string, IPurchasesResponseListener)
            var mi2 = _client!.GetType().GetMethods()
                .FirstOrDefault(m =>
                    m.Name == "QueryPurchasesAsync" &&
                    m.GetParameters().Length == 2 &&
                    m.GetParameters()[0].ParameterType == typeof(string));

            if (mi2 != null)
            {
                var tcs = new TaskCompletionSource<IList<Purchase>>();

                var listener = new PurchasesResponseListener((result, list) =>
                {
                    tcs.TrySetResult(list ?? new List<Purchase>());
                });

                mi2.Invoke(_client, new object[] { skuType, listener });
                return await tcs.Task;
            }

            // Tentativa B: QueryPurchasesAsync(string) -> PurchasesResult
            var mi1 = _client.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "QueryPurchasesAsync" && m.GetParameters().Length == 1);

            if (mi1 != null)
            {
                var ret = mi1.Invoke(_client, new object[] { skuType });
                return ExtractPurchasesListFromResult(ret);
            }

            // Tentativa C: QueryPurchases(string) sync
            var miSync = _client.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "QueryPurchases" && m.GetParameters().Length == 1);

            if (miSync != null)
            {
                var ret = miSync.Invoke(_client, new object[] { skuType });
                return ExtractPurchasesListFromResult(ret);
            }

            return new List<Purchase>();
        }

        private sealed class PurchasesResponseListener : Java.Lang.Object, IPurchasesResponseListener
        {
            private readonly Action<BillingResult?, IList<Purchase>?> _cb;
            public PurchasesResponseListener(Action<BillingResult?, IList<Purchase>?> cb) => _cb = cb;
            public void OnQueryPurchasesResponse(BillingResult? p0, IList<Purchase>? p1) => _cb(p0, p1);
        }

        private static IList<Purchase> ExtractPurchasesListFromResult(object? resultObj)
        {
            if (resultObj == null) return new List<Purchase>();

            var prop = resultObj.GetType().GetProperty("PurchasesList", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(resultObj) is IList<Purchase> list) return list;

            prop = resultObj.GetType().GetProperty("Purchases", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(resultObj) is IList<Purchase> list2) return list2;

            prop = resultObj.GetType().GetProperty("List", BindingFlags.Public | BindingFlags.Instance);
            if (prop?.GetValue(resultObj) is IList<Purchase> list3) return list3;

            return new List<Purchase>();
        }

        // =========================================================
        // RTDB: idempotência por token
        // /iapPurchases/{uid}/{purchaseToken}
        // =========================================================

        private async Task<bool> IsTokenAlreadyGrantedAsync(string uid, string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BaseUrl)) return false;

                var url = $"{BaseUrl}/iapPurchases/{Uri.EscapeDataString(uid)}/{Uri.EscapeDataString(token)}/granted.json";
                using var http = new HttpClient();
                var res = await http.GetAsync(url);
                if (!res.IsSuccessStatusCode) return false;

                var json = (await res.Content.ReadAsStringAsync())?.Trim();
                if (string.IsNullOrWhiteSpace(json) || json == "null") return false;

                return json.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private async Task MarkTokenGrantedAsync(string uid, string token, string productId, string orderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(BaseUrl)) return;

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var payload = new
                {
                    productId = productId,
                    orderId = orderId,
                    granted = true,
                    grantedAtUtcMs = nowMs
                };

                var url = $"{BaseUrl}/iapPurchases/{Uri.EscapeDataString(uid)}/{Uri.EscapeDataString(token)}.json";
                var json = JsonSerializer.Serialize(payload, _jsonOpts);

                using var http = new HttpClient();
                await http.PutAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch
            {
                // best-effort
            }
        }

        // =========================================================
        // COMPAT HELPERS (reflection)
        // =========================================================

        private static object? LaunchBillingFlowCompat(BillingClient client, Android.App.Activity act, BillingFlowParams flowParams)
        {
            var mi = client.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "LaunchBillingFlow" && m.GetParameters().Length == 2);

            if (mi != null)
                return mi.Invoke(client, new object[] { act, flowParams });

            return null;
        }

        // ✅ Retorna BillingResponseCode (enum), não int
        private static BillingResponseCode GetResponseCodeCompat(object? billingResult)
        {
            if (billingResult == null) return BillingResponseCode.Error;

            var prop = billingResult.GetType().GetProperty("ResponseCode", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return BillingResponseCode.Error;

            var val = prop.GetValue(billingResult);

            if (val is BillingResponseCode code)
                return code;

            if (val is int i)
                return (BillingResponseCode)i;

            try
            {
                var ii = Convert.ToInt32(val);
                return (BillingResponseCode)ii;
            }
            catch
            {
                return BillingResponseCode.Error;
            }
        }

        private static string? GetDebugMessageCompat(object? billingResult)
        {
            if (billingResult == null) return null;
            var prop = billingResult.GetType().GetProperty("DebugMessage", BindingFlags.Public | BindingFlags.Instance);
            return prop?.GetValue(billingResult)?.ToString();
        }

        private static void AcknowledgePurchaseCompat(BillingClient client, AcknowledgePurchaseParams p, IAcknowledgePurchaseResponseListener listener)
        {
            var mi = client.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "AcknowledgePurchase" && m.GetParameters().Length == 2);

            if (mi != null)
            {
                mi.Invoke(client, new object[] { p, listener });
                return;
            }

            throw new MissingMethodException("AcknowledgePurchase não encontrado no binding atual.");
        }

        private static void ConsumeAsyncCompat(BillingClient client, ConsumeParams p, IConsumeResponseListener listener)
        {
            var mi = client.GetType().GetMethods()
                .FirstOrDefault(m => m.Name == "ConsumeAsync" && m.GetParameters().Length == 2);

            if (mi != null)
            {
                mi.Invoke(client, new object[] { p, listener });
                return;
            }

            throw new MissingMethodException("ConsumeAsync não encontrado no binding atual.");
        }
    }
}
#endif
