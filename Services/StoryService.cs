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

        // -----------------------------
        // STORIES
        // -----------------------------

        public async Task AddStoryAsync(string userId, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId é obrigatório.");

            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentException("imageUrl é obrigatório.");

            var story = new StoryItem
            {
                UserId = userId,
                ImageUrl = imageUrl,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Likes = 0
            };

            var url = $"{BaseUrl}/stories/{userId}.json";
            var json = JsonSerializer.Serialize(story, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FirebasePushResult>(resultJson, _jsonOptions);

            var id = result?.Name ?? string.Empty;
            if (!string.IsNullOrEmpty(id))
            {
                story.Id = id;
                var putUrl = $"{BaseUrl}/stories/{userId}/{id}.json";
                var putJson = JsonSerializer.Serialize(story, _jsonOptions);
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

            var dict = JsonSerializer.Deserialize<Dictionary<string, StoryItem>>(json, _jsonOptions)
                       ?? new Dictionary<string, StoryItem>();

            var now = DateTime.UtcNow;

            var list = dict
                .Select(kv =>
                {
                    kv.Value.Id = kv.Key;
                    return kv.Value;
                })
                .Where(s => s.ExpiresAt > now) // remove expirados
                .OrderBy(s => s.CreatedAt)
                .ToList();

            return list;
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
            // Não usado mais. Mantido apenas pra compatibilidade.
            await Task.CompletedTask;
        }
    }
}
