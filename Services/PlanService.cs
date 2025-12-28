using AmoraApp.Config;
using System;
using System.Collections.Generic;
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

    public enum PlanPeriod
    {
        Monthly,
        Yearly
    }

    /// <summary>
    /// Registro completo que fica em /plans/{uid}.
    /// </summary>
    public class UserPlanRecord
    {
        public string PlanType { get; set; } = "Free";   // "Free", "Plus", "Premium"
        public string Period { get; set; } = "monthly";  // "monthly", "yearly", "coupon"
        public long StartedAtUtc { get; set; } = 0;      // Unix seconds
        public long ExpiresAtUtc { get; set; } = 0;      // Unix seconds

        public int BoostsAvailable { get; set; } = 0;
        public long LastBoostGrantUtc { get; set; } = 0;
    }

    // =========================
    // NOVO: CUPONS
    // =========================
    public class CouponRecord
    {
        public string Code { get; set; } = "";
        public string Plan { get; set; } = ""; // "Plus", "Premium", "Boosts"
        public int Days { get; set; } = 0;     // Ex.: 90
        public int Boosts { get; set; } = 0;   // Ex.: 10 (quando Plan="Boosts")

        // melhor formato no RTDB (evita duplicados e PATCH fácil):
        // users: { "uid1": true, "uid2": true }
        public Dictionary<string, bool> Users { get; set; } = new Dictionary<string, bool>();

        // true = cupom ativo (válido)
        public bool Activated { get; set; } = true;
    }

    public class CouponRedeemResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
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
        private const int PlusIncludedBoosts = 5;       // boosts incluídos ao ativar Plus
        private const int PremiumIncludedBoosts = 10;   // boosts incluídos ao ativar Premium

        private PlanService() { }

        // =========================================================
        // HELPERS GERAIS
        // =========================================================

        public string GetPlanDisplayName(PlanType plan) =>
            plan switch
            {
                PlanType.Plus => "Plus",
                PlanType.Premium => "Premium",
                _ => "Grátis"
            };

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

        private string PeriodToString(PlanPeriod period) =>
            period == PlanPeriod.Yearly ? "yearly" : "monthly";

        // =========================================================
        // ACESSO AO REGISTRO COMPLETO /plans/{uid}
        // =========================================================

        private async Task<UserPlanRecord?> GetPlanRecordAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return null;

            try
            {
                var url = $"{_baseUrl}/plans/{uid}.json";
                var res = await _http.GetAsync(url);
                if (!res.IsSuccessStatusCode)
                    return null;

                var json = await res.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return null;

                return JsonSerializer.Deserialize<UserPlanRecord>(json, _opts);
            }
            catch
            {
                return null;
            }
        }

        private async Task SavePlanRecordAsync(string uid, UserPlanRecord record)
        {
            if (string.IsNullOrWhiteSpace(uid) || record == null)
                return;

            var url = $"{_baseUrl}/plans/{uid}.json";
            var json = JsonSerializer.Serialize(record, _opts);
            await _http.PutAsync(
                url,
                new StringContent(json, Encoding.UTF8, "application/json"));
        }

        // =========================================================
        // PLANO ATUAL DO USUÁRIO
        // =========================================================

        public async Task<PlanType> GetUserPlanAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return PlanType.Free;

            try
            {
                var record = await GetPlanRecordAsync(uid);
                if (record == null)
                    return PlanType.Free;

                if (record.ExpiresAtUtc <= 0)
                    return PlanType.Free;

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now >= record.ExpiresAtUtc)
                {
                    await DowngradeToFreeAsync(uid);
                    return PlanType.Free;
                }

                return ParsePlanFromString(record.PlanType);
            }
            catch
            {
                return PlanType.Free;
            }
        }

        public async Task SetUserPlanAsync(string uid, PlanType plan)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            await ActivatePlanAsync(uid, plan, PlanPeriod.Monthly);
        }

        public async Task DowngradeToFreeAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var record = new UserPlanRecord
            {
                PlanType = "Free",
                Period = "monthly",
                StartedAtUtc = 0,
                ExpiresAtUtc = 0,
                BoostsAvailable = 0,
                LastBoostGrantUtc = 0
            };

            await SavePlanRecordAsync(uid, record);
        }

        public async Task ActivatePlanAsync(string uid, PlanType plan, PlanPeriod period)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var now = DateTimeOffset.UtcNow;
            var started = now.ToUnixTimeSeconds();

            DateTimeOffset expiresDate;
            if (period == PlanPeriod.Monthly)
                expiresDate = now.AddDays(30);
            else
                expiresDate = now.AddYears(1);

            var expires = expiresDate.ToUnixTimeSeconds();

            var includedBoosts = GetIncludedBoosts(plan);

            var record = await GetPlanRecordAsync(uid) ?? new UserPlanRecord();

            record.PlanType = plan switch
            {
                PlanType.Plus => "Plus",
                PlanType.Premium => "Premium",
                _ => "Free"
            };

            record.Period = PeriodToString(period);
            record.StartedAtUtc = started;
            record.ExpiresAtUtc = expires;

            record.BoostsAvailable += includedBoosts;
            record.LastBoostGrantUtc = started;

            await SavePlanRecordAsync(uid, record);
        }

        // =========================
        // NOVO: ATIVAR PLANO POR DIAS (CUPOM)
        // =========================
        public async Task ActivatePlanForDaysAsync(string uid, PlanType plan, int days)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            if (days <= 0)
                days = 30;

            var now = DateTimeOffset.UtcNow;
            var started = now.ToUnixTimeSeconds();
            var expires = now.AddDays(days).ToUnixTimeSeconds();

            var includedBoosts = GetIncludedBoosts(plan);

            var record = await GetPlanRecordAsync(uid) ?? new UserPlanRecord();

            record.PlanType = plan switch
            {
                PlanType.Plus => "Plus",
                PlanType.Premium => "Premium",
                _ => "Free"
            };

            record.Period = "coupon";
            record.StartedAtUtc = started;
            record.ExpiresAtUtc = expires;

            record.BoostsAvailable += includedBoosts;
            record.LastBoostGrantUtc = started;

            await SavePlanRecordAsync(uid, record);
        }

        // =========================================================
        // CUPONS
        // =========================================================
        private async Task<CouponRecord?> GetCouponAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            try
            {
                var safe = code.Trim();
                var url = $"{_baseUrl}/coupons/{safe}.json";
                var res = await _http.GetAsync(url);
                var json = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                    return null;

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return null;

                return JsonSerializer.Deserialize<CouponRecord>(json, _opts);
            }
            catch
            {
                return null;
            }
        }

        private async Task MarkCouponUsedByUserAsync(string code, string uid)
        {
            // PATCH “barato”: escreve users/{uid} = true
            var safe = code.Trim();
            var path = $"{_baseUrl}/coupons/{safe}/users/{uid}.json";
            await _http.PutAsync(path, new StringContent("true", Encoding.UTF8, "application/json"));
        }

        public async Task<CouponRedeemResult> RedeemCouponAsync(string uid, string code)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return new CouponRedeemResult { Success = false, Message = "Login necessário." };

            code = (code ?? "").Trim();
            if (string.IsNullOrWhiteSpace(code))
                return new CouponRedeemResult { Success = false, Message = "Cupom inválido." };

            var coupon = await GetCouponAsync(code);
            if (coupon == null)
                return new CouponRedeemResult { Success = false, Message = "Cupom não encontrado ou oferta inválida." };

            if (!coupon.Activated)
                return new CouponRedeemResult { Success = false, Message = "Oferta inválida ou expirada." };

            if (coupon.Users != null && coupon.Users.ContainsKey(uid))
                return new CouponRedeemResult { Success = false, Message = "Você já ativou este cupom." };

            var planStr = (coupon.Plan ?? "").Trim().ToLowerInvariant();

            if (planStr == "plus" || planStr == "premium")
            {
                var plan = planStr == "premium" ? PlanType.Premium : PlanType.Plus;
                var days = coupon.Days <= 0 ? 90 : coupon.Days;

                await ActivatePlanForDaysAsync(uid, plan, days);
                await MarkCouponUsedByUserAsync(code, uid);

                return new CouponRedeemResult
                {
                    Success = true,
                    Message = $"Cupom ativado! Plano {GetPlanDisplayName(plan)} liberado por {days} dias."
                };
            }

            if (planStr == "boosts" || planStr == "boosters" || planStr == "boost")
            {
                var qty = coupon.Boosts <= 0 ? 10 : coupon.Boosts;

                await AddUserBoostsAsync(uid, qty);
                await MarkCouponUsedByUserAsync(code, uid);

                return new CouponRedeemResult
                {
                    Success = true,
                    Message = $"Cupom ativado! Você recebeu {qty} boost(s)."
                };
            }

            return new CouponRedeemResult { Success = false, Message = "Cupom inválido." };
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
        // BOOSTS
        // =========================================================

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

        public class PlanStatusInfo
        {
            public PlanType Plan { get; set; } = PlanType.Free;
            public int RemainingDays { get; set; } = 0;
        }

        public async Task<PlanStatusInfo> GetUserPlanStatusAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return new PlanStatusInfo { Plan = PlanType.Free, RemainingDays = 0 };

            try
            {
                var record = await GetPlanRecordAsync(uid);
                if (record == null || record.ExpiresAtUtc <= 0)
                    return new PlanStatusInfo { Plan = PlanType.Free, RemainingDays = 0 };

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var secondsLeft = record.ExpiresAtUtc - now;

                if (secondsLeft <= 0)
                {
                    // expirou -> volta para Free
                    await DowngradeToFreeAsync(uid);
                    return new PlanStatusInfo { Plan = PlanType.Free, RemainingDays = 0 };
                }

                // Dias restantes (decrescente)
                var daysLeft = (int)Math.Ceiling(secondsLeft / 86400.0);
                if (daysLeft < 0) daysLeft = 0;

                return new PlanStatusInfo
                {
                    Plan = ParsePlanFromString(record.PlanType),
                    RemainingDays = daysLeft
                };
            }
            catch
            {
                return new PlanStatusInfo { Plan = PlanType.Free, RemainingDays = 0 };
            }
        }


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
