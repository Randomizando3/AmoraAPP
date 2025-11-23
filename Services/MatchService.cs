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
            PropertyNameCaseInsensitive = true // 🔥 IMPORTANTE: garante que photoUrl, jobTitle etc mapeiem em PhotoUrl, JobTitle...
        };

        public static MatchService Instance { get; } = new MatchService();

        private MatchService() { }

        // Registrar LIKE
        public async Task<bool> LikeUserAsync(string me, string target)
        {
            // 1. marca like
            await PutAsync($"/likes/{me}/{target}.json", true);

            // 2. verifica se o outro também me deu like
            var otherLikedMe = await GetAsync<bool?>($"/likes/{target}/{me}.json");

            if (otherLikedMe == true)
            {
                // É MATCH!
                await PutAsync($"/matches/{me}/{target}.json", true);
                await PutAsync($"/matches/{target}/{me}.json", true);
                return true;
            }

            return false;
        }

        // Dislike
        public async Task DislikeUserAsync(string me, string target)
        {
            await DeleteAsync($"/likes/{me}/{target}.json");
        }

        // Lista de matches
        public async Task<List<string>> GetMatchesAsync(string uid)
        {
            var res = await GetAsync<Dictionary<string, bool>>($"/matches/{uid}.json");
            if (res == null) return new();
            return res.Keys.ToList();
        }

        // Usuários sugeridos / Discover
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

        // Firebase REST helpers
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
