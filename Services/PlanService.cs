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
        public string Period { get; set; } = "monthly";  // "monthly", "yearly"
        public long StartedAtUtc { get; set; } = 0;      // Unix seconds
        public long ExpiresAtUtc { get; set; } = 0;      // Unix seconds

        public int BoostsAvailable { get; set; } = 0;
        public long LastBoostGrantUtc { get; set; } = 0;
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

        private string PeriodToString(PlanPeriod period) =>
            period == PlanPeriod.Yearly ? "yearly" : "monthly";

        private PlanPeriod ParsePeriodFromString(string? period)
        {
            if (string.IsNullOrWhiteSpace(period))
                return PlanPeriod.Monthly;

            return period.ToLowerInvariant() switch
            {
                "yearly" => PlanPeriod.Yearly,
                _ => PlanPeriod.Monthly
            };
        }

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

        /// <summary>
        /// Lê o plano atual do usuário considerando expiração.
        /// Se expirou ou não tiver registro válido, retorna Free.
        /// </summary>
        public async Task<PlanType> GetUserPlanAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return PlanType.Free;

            try
            {
                var record = await GetPlanRecordAsync(uid);
                if (record == null)
                    return PlanType.Free;

                // Se não tiver expiração definida, trata como plano inválido → Free
                if (record.ExpiresAtUtc <= 0)
                    return PlanType.Free;

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now >= record.ExpiresAtUtc)
                {
                    // Plano expirado → volta para Free
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

        /// <summary>
        /// Para compatibilidade: em vez de só setar planType, já ativa o plano como mensal.
        /// </summary>
        public async Task SetUserPlanAsync(string uid, PlanType plan)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            await ActivatePlanAsync(uid, plan, PlanPeriod.Monthly);
        }

        /// <summary>
        /// Força downgrade para Free (usado quando expira).
        /// </summary>
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

        /// <summary>
        /// Ativa um plano (Plus/Premium) com período mensal ou anual.
        /// Calcula startedAt / expiresAt e credita boosts incluídos.
        /// </summary>
        public async Task ActivatePlanAsync(string uid, PlanType plan, PlanPeriod period)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var now = DateTimeOffset.UtcNow;
            var started = now.ToUnixTimeSeconds();

            DateTimeOffset expiresDate;
            if (period == PlanPeriod.Monthly)
                expiresDate = now.AddDays(30); // 30 dias fictícios
            else
                expiresDate = now.AddYears(1); // 1 ano fictício

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

            // soma boosts incluídos no saldo (se já tinha compras avulsas, mantém)
            record.BoostsAvailable += includedBoosts;
            record.LastBoostGrantUtc = started;

            await SavePlanRecordAsync(uid, record);
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
