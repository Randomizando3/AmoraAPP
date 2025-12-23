using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AmoraApp.Config;
using AmoraApp.Models;

namespace AmoraApp.Services
{
    public class StoryService
    {
        public static StoryService Instance { get; } = new StoryService();

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        private string BaseUrl => FirebaseSettings.DatabaseUrl.TrimEnd('/');

        // 24 horas, estilo Instagram
        private const long StoryTtlSeconds = 24 * 60 * 60;

        private StoryService()
        {
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        private class FirebasePushResult
        {
            public string Name { get; set; } = string.Empty;
        }

        /// <summary>
        /// DTO interno para suportar dados antigos (DateTime) e novos (Unix seconds)
        /// sem precisar mexer no StoryItem agora.
        /// </summary>
        private sealed class StoryItemDto
        {
            public string Id { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
            public string ImageUrl { get; set; } = string.Empty;

            public DateTime? CreatedAt { get; set; }
            public DateTime? ExpiresAt { get; set; }

            // NOVOS (mais robustos)
            public long? CreatedAtUtc { get; set; }
            public long? ExpiresAtUtc { get; set; }

            public int Likes { get; set; } = 0;
        }

        // -----------------------------
        // STORIES
        // -----------------------------

        public async Task AddStoryAsync(string userId, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId é obrigatório.");

            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("imageUrl é obrigatório.");

            var nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expiresUtc = nowUtc + StoryTtlSeconds;

            // Mantém os campos antigos (DateTime) + adiciona campos robustos (Unix seconds)
            var storyDto = new StoryItemDto
            {
                UserId = userId,
                ImageUrl = imageUrl,

                CreatedAt = DateTimeOffset.FromUnixTimeSeconds(nowUtc).UtcDateTime,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUtc).UtcDateTime,

                CreatedAtUtc = nowUtc,
                ExpiresAtUtc = expiresUtc,

                Likes = 0
            };

            var url = $"{BaseUrl}/stories/{userId}.json";
            var json = JsonSerializer.Serialize(storyDto, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FirebasePushResult>(resultJson, _jsonOptions);

            var id = result?.Name ?? string.Empty;
            if (!string.IsNullOrEmpty(id))
            {
                storyDto.Id = id;

                // grava com ID dentro do nó também
                var putUrl = $"{BaseUrl}/stories/{userId}/{id}.json";
                var putJson = JsonSerializer.Serialize(storyDto, _jsonOptions);
                var putContent = new StringContent(putJson, Encoding.UTF8, "application/json");
                await _httpClient.PutAsync(putUrl, putContent);
            }
        }

        public async Task<IList<StoryItem>> GetStoriesAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return new List<StoryItem>();

            var url = $"{BaseUrl}/stories/{userId}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<StoryItem>();

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<StoryItem>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, StoryItemDto>>(json, _jsonOptions)
                       ?? new Dictionary<string, StoryItemDto>();

            var nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var valid = new List<StoryItem>();

            foreach (var kv in dict)
            {
                var id = kv.Key;
                var dto = kv.Value ?? new StoryItemDto();
                dto.Id = string.IsNullOrWhiteSpace(dto.Id) ? id : dto.Id;

                // Normaliza timestamps (prioriza Unix seconds, cai pro DateTime)
                var createdUtc = ResolveCreatedUtc(dto);
                var expiresUtc = ResolveExpiresUtc(dto, createdUtc);

                // Se não conseguir resolver, considera expirado (seguro para "máx 24h")
                if (expiresUtc <= 0)
                {
                    _ = CleanupExpiredAsync(userId, dto.Id);
                    continue;
                }

                if (expiresUtc <= nowUtc)
                {
                    _ = CleanupExpiredAsync(userId, dto.Id);
                    continue;
                }

                // Mapeia para seu StoryItem atual
                var item = new StoryItem
                {
                    Id = dto.Id,
                    UserId = string.IsNullOrWhiteSpace(dto.UserId) ? userId : dto.UserId,
                    ImageUrl = dto.ImageUrl ?? string.Empty,

                    // Mantém DateTime preenchido para telas que usam
                    CreatedAt = DateTimeOffset.FromUnixTimeSeconds(createdUtc > 0 ? createdUtc : nowUtc).UtcDateTime,
                    ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expiresUtc).UtcDateTime,

                    Likes = dto.Likes
                };

                valid.Add(item);
            }

            // Ordena do mais antigo pro mais novo (como stories em sequência)
            return valid.OrderBy(s => s.CreatedAt).ToList();
        }

        private long ResolveCreatedUtc(StoryItemDto dto)
        {
            if (dto.CreatedAtUtc.HasValue && dto.CreatedAtUtc.Value > 0)
                return dto.CreatedAtUtc.Value;

            if (dto.CreatedAt.HasValue && dto.CreatedAt.Value != default)
            {
                // Trata como UTC (porque você grava em UTC)
                var d = DateTime.SpecifyKind(dto.CreatedAt.Value, DateTimeKind.Utc);
                return new DateTimeOffset(d).ToUnixTimeSeconds();
            }

            return 0;
        }

        private long ResolveExpiresUtc(StoryItemDto dto, long createdUtc)
        {
            if (dto.ExpiresAtUtc.HasValue && dto.ExpiresAtUtc.Value > 0)
                return dto.ExpiresAtUtc.Value;

            if (dto.ExpiresAt.HasValue && dto.ExpiresAt.Value != default)
            {
                // Trata como UTC (porque você grava em UTC)
                var d = DateTime.SpecifyKind(dto.ExpiresAt.Value, DateTimeKind.Utc);
                return new DateTimeOffset(d).ToUnixTimeSeconds();
            }

            // Se tiver createdUtc mas não tiver expires, calcula TTL
            if (createdUtc > 0)
                return createdUtc + StoryTtlSeconds;

            return 0;
        }

        private async Task CleanupExpiredAsync(string ownerUserId, string storyId)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId) || string.IsNullOrWhiteSpace(storyId))
                return;

            try
            {
                // Remove story expirado
                var storyUrl = $"{BaseUrl}/stories/{ownerUserId}/{storyId}.json";
                await _httpClient.DeleteAsync(storyUrl);

                // Remove likes do story expirado (opcional, mas deixa o DB limpo)
                var likesUrl = $"{BaseUrl}/storyLikes/{ownerUserId}/{storyId}.json";
                await _httpClient.DeleteAsync(likesUrl);
            }
            catch
            {
                // best-effort: não quebra o fluxo de listagem
            }
        }

        // -----------------------------
        // LIKES POR USUÁRIO
        // /storyLikes/{ownerUserId}/{storyId}/{likerUserId} = true
        // -----------------------------

        public async Task<(int likes, bool likedByMe)> GetStoryLikesAsync(
            string ownerUserId,
            string storyId,
            string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId) || string.IsNullOrWhiteSpace(storyId))
                return (0, false);

            var url = $"{BaseUrl}/storyLikes/{ownerUserId}/{storyId}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return (0, false);

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return (0, false);

            var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json, _jsonOptions)
                       ?? new Dictionary<string, bool>();

            var likes = dict.Count;
            var likedByMe = !string.IsNullOrEmpty(currentUserId) && dict.ContainsKey(currentUserId);

            return (likes, likedByMe);
        }

        public async Task ToggleLikeAsync(string ownerUserId, string storyId, string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId) ||
                string.IsNullOrWhiteSpace(storyId) ||
                string.IsNullOrWhiteSpace(currentUserId))
                return;

            var path = $"/storyLikes/{ownerUserId}/{storyId}/{currentUserId}.json";
            var url = $"{BaseUrl}{path}";

            // Verifica se já existe like
            var checkResponse = await _httpClient.GetAsync(url);
            if (checkResponse.IsSuccessStatusCode)
            {
                var json = await checkResponse.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(json) && json != "null")
                {
                    // Já tinha like -> remover (unlike)
                    await _httpClient.DeleteAsync(url);
                    return;
                }
            }

            // Não tinha like -> adicionar
            var content = new StringContent("true", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(url, content);
        }

        // Método legado, se ainda for chamado em algum lugar não quebra
        public async Task LikeStoryAsync(string ownerUserId, string storyId)
        {
            await Task.CompletedTask;
        }
    }
}
