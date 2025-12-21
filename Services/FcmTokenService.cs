using AmoraApp.Config;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    public static class FcmTokenService
    {
        private static readonly HttpClient _http = new();
        private static readonly string BaseUrl = FirebaseSettings.DatabaseUrl.TrimEnd('/');

        public static async Task UpsertAsync(string uid, string token, string platform)
        {
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
                return;

            // Evita caracteres problemáticos em keys do RTDB: usa hash como key e guarda o token no valor
            var key = Sha1Hex(token);

            var payload = new
            {
                token,
                platform,
                updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _http.PutAsync($"{BaseUrl}/fcmTokens/{uid}/{key}.json", content);
        }

        private static string Sha1Hex(string value)
        {
            using var sha1 = SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
