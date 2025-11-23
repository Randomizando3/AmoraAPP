using AmoraApp.Config;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    public class PresenceService
    {
        public static PresenceService Instance { get; } = new PresenceService();

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        private string BaseUrl => FirebaseSettings.DatabaseUrl.TrimEnd('/');

        private PresenceService()
        {
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        private class PresenceDto
        {
            public bool IsOnline { get; set; }
            public long LastSeenUtc { get; set; }
        }

        /// <summary>
        /// Marca o usuário como online em /presence/{userId}.
        /// </summary>
        public async Task SetOnlineAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var dto = new PresenceDto
            {
                IsOnline = true,
                LastSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var json = JsonSerializer.Serialize(dto, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{BaseUrl}/presence/{userId}.json";
            await _httpClient.PutAsync(url, content);
        }

        /// <summary>
        /// Marca o usuário como offline (atualiza lastSeenUtc).
        /// </summary>
        public async Task SetOfflineAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            var dto = new PresenceDto
            {
                IsOnline = false,
                LastSeenUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var json = JsonSerializer.Serialize(dto, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{BaseUrl}/presence/{userId}.json";
            await _httpClient.PutAsync(url, content);
        }

        /// <summary>
        /// Retorna presença completa (IsOnline + LastSeenUtc) ou null.
        /// </summary>
        public async Task<(bool IsOnline, long LastSeenUtc)?> GetPresenceAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return null;

            var url = $"{BaseUrl}/presence/{userId}.json";
            var resp = await _httpClient.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
                return null;

            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            var dto = JsonSerializer.Deserialize<PresenceDto>(json, _jsonOptions);
            if (dto == null)
                return null;

            return (dto.IsOnline, dto.LastSeenUtc);
        }
    }
}
