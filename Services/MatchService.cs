using AmoraApp.Config;
using AmoraApp.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    public class MatchService
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly string BaseUrl = FirebaseSettings.DatabaseUrl.TrimEnd('/');

        private readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true // garante que photoUrl, jobTitle etc mapeiem bem
        };

        public static MatchService Instance { get; } = new MatchService();

        private MatchService() { }

        // =========================================================
        // LIKE / DISLIKE / MATCH
        // =========================================================

        /// <summary>
        /// Registra um LIKE. Se o outro já tiver dado LIKE em mim, vira MATCH.
        /// Além de /likes, também grava /likesReceived[target][me].
        /// </summary>
        public async Task<bool> LikeUserAsync(string me, string target)
        {
            if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("me e target são obrigatórios.");

            // 1) registra meu like
            await PutAsync($"/likes/{me}/{target}.json", true);

            // 2) registra que o alvo recebeu um like meu
            await PutAsync($"/likesReceived/{target}/{me}.json", true);

            // 3) verifica se o outro já tinha me curtido
            var otherLikedMe = await GetAsync<bool?>($"/likes/{target}/{me}.json");

            if (otherLikedMe == true)
            {
                // MATCH pros dois lados
                await PutAsync($"/matches/{me}/{target}.json", true);
                await PutAsync($"/matches/{target}/{me}.json", true);

                // opcional: limpar pendências de likesReceived
                await DeleteAsync($"/likesReceived/{me}/{target}.json");
                await DeleteAsync($"/likesReceived/{target}/{me}.json");

                return true;
            }

            return false;
        }

        /// <summary>
        /// Dislike: remove meu like no outro.
        /// (Não mexe nos likes que ele me deu.)
        /// </summary>
        public async Task DislikeUserAsync(string me, string target)
        {
            if (string.IsNullOrWhiteSpace(me) || string.IsNullOrWhiteSpace(target))
                return;

            await DeleteAsync($"/likes/{me}/{target}.json");
        }

        /// <summary>
        /// Lista de IDs com quem já é MATCH.
        /// </summary>
        public async Task<List<string>> GetMatchesAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return new();

            var res = await GetAsync<Dictionary<string, bool>>($"/matches/{uid}.json");
            if (res == null) return new();
            return res.Keys.ToList();
        }

        // =========================================================
        // QUEM ME CURTIU
        // =========================================================

        /// <summary>
        /// Retorna a lista de perfis que já curtiram o usuário,
        /// baseada em /likesReceived/{myUid}/{likerId} = true.
        /// </summary>
        public async Task<List<UserProfile>> GetUsersWhoLikedMeAsync(string myUid)
        {
            var result = new List<UserProfile>();

            if (string.IsNullOrWhiteSpace(myUid))
                return result;

            // /likesReceived/{myUid} = { likerId: true, ... }
            var dict = await GetAsync<Dictionary<string, bool>>($"/likesReceived/{myUid}.json");
            if (dict == null || dict.Count == 0)
                return result;

            // (opcional) evita mostrar quem já é match
            var myMatches = await GetMatchesAsync(myUid);
            var matchSet = new HashSet<string>(myMatches);

            foreach (var likerId in dict.Keys)
            {
                if (string.IsNullOrWhiteSpace(likerId))
                    continue;

                // se já é match, você pode escolher esconder daqui
                if (matchSet.Contains(likerId))
                    continue;

                var profile = await FirebaseDatabaseService.Instance.GetUserProfileAsync(likerId);
                if (profile == null)
                    continue;

                profile.Id = likerId;
                profile.PhotoUrl ??= string.Empty;
                profile.Gallery ??= new List<string>();
                profile.Interests ??= new List<string>();

                result.Add(profile);
            }

            // opcional: ordenar por nome, idade, etc.
            return result.OrderByDescending(p => p.Id).ToList();
        }

        // =========================================================
        // DISCOVER
        // =========================================================

        /// <summary>
        /// Usuários sugeridos para Discover.
        /// </summary>
        public async Task<List<UserProfile>> GetUsersForDiscoverAsync(string myUid)
        {
            var allUsers = await GetAsync<Dictionary<string, UserProfile>>("/users.json");

            if (allUsers == null) return new();

            var list = allUsers
                .Where(k => k.Key != myUid)
                .Select(k =>
                {
                    var u = k.Value ?? new UserProfile();
                    u.Id = k.Key;

                    // Garante nunca nulo
                    u.PhotoUrl ??= string.Empty;
                    u.Gallery ??= new List<string>();
                    u.Interests ??= new List<string>();

                    return u;
                })
                .ToList();

            return list;
        }

        // =========================================================
        // HELPERS FIREBASE REST
        // =========================================================

        private async Task<T?> GetAsync<T>(string path)
        {
            var res = await _http.GetAsync(BaseUrl + path);
            if (!res.IsSuccessStatusCode) return default;
            var json = await res.Content.ReadAsStringAsync();
            if (json == "null") return default;
            return JsonSerializer.Deserialize<T>(json, _opts);
        }

        private async Task<bool> PutAsync(string path, object value)
        {
            var json = JsonSerializer.Serialize(value, _opts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _http.PutAsync(BaseUrl + path, content);
            return res.IsSuccessStatusCode;
        }

        private async Task DeleteAsync(string path)
        {
            await _http.DeleteAsync(BaseUrl + path);
        }
    }
}
