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
    public class FirebaseDatabaseService
    {
        public static FirebaseDatabaseService Instance { get; } = new FirebaseDatabaseService();

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        private string BaseUrl => FirebaseSettings.DatabaseUrl.TrimEnd('/');

        private FirebaseDatabaseService()
        {
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        // ============================================================
        // USERS
        // ============================================================

        public async Task SaveUserProfileAsync(UserProfile profile)
        {
            if (string.IsNullOrEmpty(profile.Id))
                throw new ArgumentException("UserProfile.Id precisa ser o uid do Firebase");

            var url = $"{BaseUrl}/users/{profile.Id}.json";
            var json = JsonSerializer.Serialize(profile, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<UserProfile?> GetUserProfileAsync(string uid)
        {
            var url = $"{BaseUrl}/users/{uid}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            return JsonSerializer.Deserialize<UserProfile>(json, _jsonOptions);
        }

        // ============================================================
        // POSTS
        // ============================================================

        private class FirebasePostPushResult
        {
            public string Name { get; set; } = string.Empty;
        }

        public async Task<string> CreatePostAsync(Post post)
        {
            var url = $"{BaseUrl}/posts.json";
            var json = JsonSerializer.Serialize(post, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FirebasePostPushResult>(resultJson, _jsonOptions);

            var id = result?.Name ?? string.Empty;

            if (!string.IsNullOrEmpty(id))
            {
                post.Id = id;

                var updateUrl = $"{BaseUrl}/posts/{id}.json";
                var updateJson = JsonSerializer.Serialize(post, _jsonOptions);
                var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

                await _httpClient.PutAsync(updateUrl, updateContent);
            }

            return id;
        }

        public async Task<IList<Post>> GetRecentPostsAsync(int limit = 20)
        {
            var url = $"{BaseUrl}/posts.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<Post>();

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<Post>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, Post>>(json, _jsonOptions)
                       ?? new Dictionary<string, Post>();

            return dict
                .Select(kv =>
                {
                    kv.Value.Id = kv.Key;
                    return kv.Value;
                })
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToList();
        }

        // FEED DE AMIGOS (MATCHES + VOCÊ)
        public async Task<IList<Post>> GetFriendsPostsAsync(
            string myUid,
            IList<string> friendIds,
            int limit = 50)
        {
            var url = $"{BaseUrl}/posts.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<Post>();

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<Post>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, Post>>(json, _jsonOptions)
                       ?? new Dictionary<string, Post>();

            // 🚫 CORRIGIDO! — troca da linha que gerava o erro CS1525
            var friendSet = new HashSet<string>(friendIds ?? Array.Empty<string>());
            friendSet.Add(myUid);

            return dict
                .Select(kv =>
                {
                    kv.Value.Id = kv.Key;
                    return kv.Value;
                })
                .Where(p => friendSet.Contains(p.UserId))
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public async Task LikePostAsync(string postId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return;

            var getUrl = $"{BaseUrl}/posts/{postId}.json";
            var response = await _httpClient.GetAsync(getUrl);

            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return;

            var post = JsonSerializer.Deserialize<Post>(json, _jsonOptions);
            if (post == null)
                return;

            post.Likes++;

            var putUrl = $"{BaseUrl}/posts/{postId}.json";
            var putJson = JsonSerializer.Serialize(post, _jsonOptions);
            var content = new StringContent(putJson, Encoding.UTF8, "application/json");

            await _httpClient.PutAsync(putUrl, content);
        }

        // ============================================================
        // COMMENTS
        // ============================================================

        public async Task AddCommentAsync(Comment comment)
        {
            if (string.IsNullOrWhiteSpace(comment.PostId))
                throw new ArgumentException("Comment.PostId é obrigatório.");

            var url = $"{BaseUrl}/comments/{comment.PostId}.json";
            var json = JsonSerializer.Serialize(comment, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            await _httpClient.PostAsync(url, content);

            // Atualizar contador de comentários
            var postUrl = $"{BaseUrl}/posts/{comment.PostId}.json";
            var response = await _httpClient.GetAsync(postUrl);

            if (!response.IsSuccessStatusCode)
                return;

            var postJson = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(postJson) || postJson == "null")
                return;

            var post = JsonSerializer.Deserialize<Post>(postJson, _jsonOptions);
            if (post == null)
                return;

            post.CommentsCount++;

            var putJson = JsonSerializer.Serialize(post, _jsonOptions);
            var putContent = new StringContent(putJson, Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(postUrl, putContent);
        }

        public async Task<IList<Comment>> GetCommentsAsync(string postId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return new List<Comment>();

            var url = $"{BaseUrl}/comments/{postId}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<Comment>();

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<Comment>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, Comment>>(json, _jsonOptions)
                       ?? new Dictionary<string, Comment>();

            return dict
                .Select(kv =>
                {
                    kv.Value.Id = kv.Key;
                    return kv.Value;
                })
                .OrderBy(c => c.CreatedAt)
                .ToList();
        }


        // ------------ LIKES DE POST (POR USUÁRIO) ------------

        // /postLikes/{postId}/{userId} = true

        public async Task<(int likes, bool likedByMe)> GetPostLikesAsync(string postId, string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return (0, false);

            var url = $"{BaseUrl}/postLikes/{postId}.json";
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

        public async Task TogglePostLikeAsync(string postId, string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(postId) || string.IsNullOrWhiteSpace(currentUserId))
                return;

            var path = $"/postLikes/{postId}/{currentUserId}.json";
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

    }
}
