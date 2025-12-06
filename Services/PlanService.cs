using AmoraApp.Config;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    public enum PlanType
    {
        Free,
        Plus,
        Premium
    }

    public class PlanService
    {
        public static PlanService Instance { get; } = new PlanService();

        private readonly HttpClient _http = new HttpClient();
        private readonly string _baseUrl = FirebaseSettings.DatabaseUrl.TrimEnd('/');
        private readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Aqui você pode ajustar à vontade
        private const int FreeDailyLikeLimit = 50;      // likes/dia no plano grátis
        private const int PlusIncludedBoosts = 5;       // boosts incluídos no Plus (por mês, se quiser)
        private const int PremiumIncludedBoosts = 10;   // boosts incluídos no Premium

        private PlanService() { }

        // =========================================================
        // HELPERS GERAIS
        // =========================================================

        /// <summary>
        /// Nome bonitinho do plano para exibir na UI.
        /// </summary>
        public string GetPlanDisplayName(PlanType plan) =>
            plan switch
            {
                PlanType.Plus => "Plus",
                PlanType.Premium => "Premium",
                _ => "Grátis"
            };

        /// <summary>
        /// Converte string de banco para enum.
        /// </summary>
        public PlanType ParsePlanFromString(string? plan)
        {
            if (string.IsNullOrWhiteSpace(plan))
                return PlanType.Free;

            return plan.ToLowerInvariant() switch
            {
                "plus" => PlanType.Plus,
                "premium" => PlanType.Premium,
                _ => PlanType.Free
            };
        }

        // =========================================================
        // PLANO ATUAL DO USUÁRIO
        // =========================================================

        /// <summary>
        /// Lê o plano atual do usuário em /plans/{uid}/planType.
        /// </summary>
        public async Task<PlanType> GetUserPlanAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return PlanType.Free;

            try
            {
                var url = $"{_baseUrl}/plans/{uid}/planType.json";
                var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode)
                    return PlanType.Free;

                var json = await res.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return PlanType.Free;

                var str = JsonSerializer.Deserialize<string>(json, _opts) ?? "Free";

                return str.ToLowerInvariant() switch
                {
                    "plus" => PlanType.Plus,
                    "premium" => PlanType.Premium,
                    _ => PlanType.Free
                };
            }
            catch
            {
                return PlanType.Free;
            }
        }

        /// <summary>
        /// Define o plano do usuário em /plans/{uid}/planType.
        /// (Útil quando você integrar pagamento ou quiser simular upgrade.)
        /// </summary>
        public async Task SetUserPlanAsync(string uid, PlanType plan)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var planStr = plan switch
            {
                PlanType.Plus => "Plus",
                PlanType.Premium => "Premium",
                _ => "Free"
            };

            var url = $"{_baseUrl}/plans/{uid}/planType.json";
            var json = JsonSerializer.Serialize(planStr, _opts);
            await _http.PutAsync(
                url,
                new StringContent(json, Encoding.UTF8, "application/json"));
        }

        // =========================================================
        // FLAGS DE RECURSOS POR PLANO
        // =========================================================

        public bool HasUnlimitedLikes(PlanType plan) =>
            plan == PlanType.Plus || plan == PlanType.Premium;

        public bool CanSeeLikesReceived(PlanType plan) =>
            plan == PlanType.Plus || plan == PlanType.Premium;

        public bool CanCreateGroups(PlanType plan) =>
            plan == PlanType.Plus || plan == PlanType.Premium;

        public bool CanRewind(PlanType plan) =>
            plan == PlanType.Plus || plan == PlanType.Premium;

        public bool CanUploadVideos(PlanType plan) =>
            plan == PlanType.Premium;

        public int GetIncludedBoosts(PlanType plan) =>
            plan switch
            {
                PlanType.Plus => PlusIncludedBoosts,
                PlanType.Premium => PremiumIncludedBoosts,
                _ => 0
            };

        public bool ShowsAds(PlanType plan) =>
            plan == PlanType.Free;

        // =========================================================
        // LIKES DIÁRIOS (PLANO GRÁTIS)
        // =========================================================

        private string TodayKeyUtc() =>
            DateTime.UtcNow.ToString("yyyyMMdd");

        /// <summary>
        /// Verifica se usuário ainda pode dar like hoje.
        /// </summary>
        public async Task<bool> CanUseLikeAsync(string uid)
        {
            var plan = await GetUserPlanAsync(uid);
            if (HasUnlimitedLikes(plan))
                return true;

            var day = TodayKeyUtc();
            var path = $"{_baseUrl}/planUsage/{uid}/likes/{day}.json";
            var res = await _http.GetAsync(path);
            var json = await res.Content.ReadAsStringAsync();

            int count = 0;
            if (!string.IsNullOrWhiteSpace(json) && json != "null")
                int.TryParse(json, out count);

            return count < FreeDailyLikeLimit;
        }

        /// <summary>
        /// Registra que o usuário usou 1 like hoje (somente plano grátis).
        /// </summary>
        public async Task RegisterLikeAsync(string uid)
        {
            var plan = await GetUserPlanAsync(uid);
            if (HasUnlimitedLikes(plan))
                return;

            var day = TodayKeyUtc();
            var path = $"{_baseUrl}/planUsage/{uid}/likes/{day}.json";
            var res = await _http.GetAsync(path);
            var json = await res.Content.ReadAsStringAsync();

            int count = 0;
            if (!string.IsNullOrWhiteSpace(json) && json != "null")
                int.TryParse(json, out count);

            count++;

            await _http.PutAsync(
                path,
                new StringContent(count.ToString(), Encoding.UTF8, "application/json"));
        }

        // =========================================================
        // BOOSTS (SALDOS AVULSOS + INCLUÍDOS)
        // =========================================================

        /// <summary>
        /// Lê quantos boosts o usuário tem disponíveis em /plans/{uid}/boostsAvailable.
        /// </summary>
        public async Task<int> GetUserBoostsAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return 0;

            try
            {
                var url = $"{_baseUrl}/plans/{uid}/boostsAvailable.json";
                var res = await _http.GetAsync(url);
                var json = await res.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return 0;

                int value = 0;
                int.TryParse(json, out value);
                return value;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Soma boosts ao saldo do usuário (usado para planos ou compras avulsas).
        /// </summary>
        public async Task AddUserBoostsAsync(string uid, int quantity)
        {
            if (string.IsNullOrWhiteSpace(uid) || quantity <= 0)
                return;

            var current = await GetUserBoostsAsync(uid);
            var newValue = current + quantity;

            var url = $"{_baseUrl}/plans/{uid}/boostsAvailable.json";
            await _http.PutAsync(
                url,
                new StringContent(newValue.ToString(), Encoding.UTF8, "application/json"));
        }

        /// <summary>
        /// Consome 1 boost se houver saldo. Retorna true se conseguiu usar.
        /// </summary>
        public async Task<bool> ConsumeBoostAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return false;

            var current = await GetUserBoostsAsync(uid);
            if (current <= 0)
                return false;

            var newValue = current - 1;
            var url = $"{_baseUrl}/plans/{uid}/boostsAvailable.json";
            await _http.PutAsync(
                url,
                new StringContent(newValue.ToString(), Encoding.UTF8, "application/json"));

            return true;
        }
    }
}
