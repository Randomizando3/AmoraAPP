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

        private string BaseUrl
        {
            get
            {
                var raw = (FirebaseSettings.DatabaseUrl ?? "").Trim();
                if (string.IsNullOrWhiteSpace(raw))
                    throw new InvalidOperationException("FirebaseSettings.DatabaseUrl não configurado.");
                return raw.TrimEnd('/');
            }
        }

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
        // HELPERS (AUTH / HTTP)
        // ============================================================

        private string WithAuth(string url, string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return url;

            var sep = url.Contains("?") ? "&" : "?";
            return url + $"{sep}auth={Uri.EscapeDataString(token)}";
        }

        private async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = "";
            try { body = await response.Content.ReadAsStringAsync(); } catch { }

            throw new Exception($"Firebase RTDB error: HTTP {(int)response.StatusCode} - {response.ReasonPhrase}\n{body}");
        }

        private class FirebasePushResult
        {
            public string Name { get; set; } = string.Empty;
        }

        private async Task PatchAsync(string url, object patch, string? token)
        {
            var json = JsonSerializer.Serialize(patch, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(new HttpMethod("PATCH"), WithAuth(url, token))
            {
                Content = content
            };
            var resp = await _httpClient.SendAsync(req);
            await EnsureSuccessAsync(resp);
        }

        // ============================================================
        // USERS
        // ============================================================

        public async Task SaveUserProfileAsync(UserProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (string.IsNullOrWhiteSpace(profile.Id))
                throw new ArgumentException("UserProfile.Id precisa ser o uid do Firebase");

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var uid = Uri.EscapeDataString(profile.Id);

            var url = WithAuth($"{BaseUrl}/users/{uid}.json", token);
            var json = JsonSerializer.Serialize(profile, _jsonOptions);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            await EnsureSuccessAsync(response);
        }

        public async Task<UserProfile?> GetUserProfileAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid)) return null;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            var url = WithAuth($"{BaseUrl}/users/{u}.json", token);
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            var profile = JsonSerializer.Deserialize<UserProfile>(json, _jsonOptions);
            if (profile != null && string.IsNullOrWhiteSpace(profile.Id))
                profile.Id = uid;

            return profile;
        }

        // ============================================================
        // POSTS
        // ============================================================

        public async Task<string> CreatePostAsync(Post post)
        {
            if (post == null) throw new ArgumentNullException(nameof(post));

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();

            var url = WithAuth($"{BaseUrl}/posts.json", token);
            var json = JsonSerializer.Serialize(post, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            await EnsureSuccessAsync(response);

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FirebasePushResult>(resultJson, _jsonOptions);

            var id = result?.Name ?? string.Empty;

            if (!string.IsNullOrEmpty(id))
            {
                post.Id = id;

                var updateUrl = WithAuth($"{BaseUrl}/posts/{Uri.EscapeDataString(id)}.json", token);
                var updateJson = JsonSerializer.Serialize(post, _jsonOptions);
                using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

                var put = await _httpClient.PutAsync(updateUrl, updateContent);
                await EnsureSuccessAsync(put);
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

            var list = new List<Post>(dict.Count);

            foreach (var kv in dict)
            {
                var p = kv.Value ?? new Post();
                p.Id = kv.Key;
                list.Add(p);
            }

            return list
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public async Task<IList<Post>> GetFriendsPostsAsync(string myUid, IList<string> friendIds, int limit = 50)
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

            var friendSet = new HashSet<string>(friendIds ?? Array.Empty<string>());
            if (!string.IsNullOrWhiteSpace(myUid))
                friendSet.Add(myUid);

            var list = new List<Post>();

            foreach (var kv in dict)
            {
                var p = kv.Value ?? new Post();
                p.Id = kv.Key;

                if (!string.IsNullOrWhiteSpace(p.UserId) && friendSet.Contains(p.UserId))
                    list.Add(p);
            }

            return list
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public async Task LikePostAsync(string postId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var pid = Uri.EscapeDataString(postId);

            var getUrl = WithAuth($"{BaseUrl}/posts/{pid}.json", token);
            var response = await _httpClient.GetAsync(getUrl);

            if (!response.IsSuccessStatusCode)
                return;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return;

            var post = JsonSerializer.Deserialize<Post>(json, _jsonOptions);
            if (post == null) return;

            post.Likes++;

            var putUrl = WithAuth($"{BaseUrl}/posts/{pid}.json", token);
            var putJson = JsonSerializer.Serialize(post, _jsonOptions);
            using var content = new StringContent(putJson, Encoding.UTF8, "application/json");

            await _httpClient.PutAsync(putUrl, content);
        }

        // ============================================================
        // COMMENTS
        // ============================================================

        public async Task AddCommentAsync(Comment comment)
        {
            if (comment == null) throw new ArgumentNullException(nameof(comment));
            if (string.IsNullOrWhiteSpace(comment.PostId))
                throw new ArgumentException("Comment.PostId é obrigatório.");

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var pid = Uri.EscapeDataString(comment.PostId);

            var url = WithAuth($"{BaseUrl}/comments/{pid}.json", token);
            var json = JsonSerializer.Serialize(comment, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var postResp = await _httpClient.PostAsync(url, content);
            await EnsureSuccessAsync(postResp);

            // incrementa CommentsCount no post (best-effort)
            var postUrl = WithAuth($"{BaseUrl}/posts/{pid}.json", token);
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
            using var putContent = new StringContent(putJson, Encoding.UTF8, "application/json");

            await _httpClient.PutAsync(postUrl, putContent);
        }

        public async Task<IList<Comment>> GetCommentsAsync(string postId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return new List<Comment>();

            var pid = Uri.EscapeDataString(postId);
            var url = $"{BaseUrl}/comments/{pid}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<Comment>();

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<Comment>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, Comment>>(json, _jsonOptions)
                       ?? new Dictionary<string, Comment>();

            var list = new List<Comment>(dict.Count);

            foreach (var kv in dict)
            {
                var c = kv.Value ?? new Comment();
                c.Id = kv.Key;
                list.Add(c);
            }

            return list
                .OrderBy(c => c.CreatedAt)
                .ToList();
        }

        // ============================================================
        // POST LIKES (POR USUÁRIO)
        // ============================================================

        public async Task<(int likes, bool likedByMe)> GetPostLikesAsync(string postId, string currentUserId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return (0, false);

            var pid = Uri.EscapeDataString(postId);

            var url = $"{BaseUrl}/postLikes/{pid}.json";
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

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();

            var pid = Uri.EscapeDataString(postId);
            var uid = Uri.EscapeDataString(currentUserId);

            var url = WithAuth($"{BaseUrl}/postLikes/{pid}/{uid}.json", token);

            var checkResponse = await _httpClient.GetAsync(url);
            if (checkResponse.IsSuccessStatusCode)
            {
                var json = await checkResponse.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(json) && json != "null")
                {
                    await _httpClient.DeleteAsync(url);
                    return;
                }
            }

            using var content = new StringContent("true", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(url, content);
        }

        // ============================================================
        // CHAT - FOTO DO CHAT
        // ============================================================

        public async Task UpdateChatPhotoAsync(string chatId, string photoUrl)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var cid = Uri.EscapeDataString(chatId);

            var url = WithAuth($"{BaseUrl}/chats/{cid}/photoUrl.json", token);
            using var content = new StringContent($"\"{photoUrl}\"", Encoding.UTF8, "application/json");

            await _httpClient.PutAsync(url, content);
        }

        // ============================================================
        // REPORTS (DENÚNCIAS)
        // ============================================================

        public async Task<string> CreateReportAsync(ReportItem report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();

            // GARANTE CAMPOS IMPORTANTES (não quebra nada se já existir no model)
            // Se no seu ReportItem não existir Status/AdminAction/AdminNote, remova essas linhas.
            if (string.IsNullOrWhiteSpace(report.Status)) report.Status = "open";
            if (string.IsNullOrWhiteSpace(report.AdminAction)) report.AdminAction = "none";
            if (report.CreatedAtUtcMs <= 0) report.CreatedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var url = WithAuth($"{BaseUrl}/reports.json", token);
            var json = JsonSerializer.Serialize(report, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            await EnsureSuccessAsync(response);

            var resultJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FirebasePushResult>(resultJson, _jsonOptions);

            var id = result?.Name ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(id))
            {
                report.Id = id;

                // Faz um PUT com o objeto completo (incluindo Id) no nó final.
                var putUrl = WithAuth($"{BaseUrl}/reports/{Uri.EscapeDataString(id)}.json", token);
                var putJson = JsonSerializer.Serialize(report, _jsonOptions);
                using var putContent = new StringContent(putJson, Encoding.UTF8, "application/json");

                var putResp = await _httpClient.PutAsync(putUrl, putContent);
                await EnsureSuccessAsync(putResp);
            }

            return id;
        }

        public async Task<IList<ReportItem>> GetAllReportsAsync(int limit = 300)
        {
            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();

            // IMPORTANTE:
            // Não usa orderBy/limitToLast para não depender de indexOn nas Rules.
            // Puxa tudo e ordena localmente.
            var url = WithAuth($"{BaseUrl}/reports.json", token);

            var resp = await _httpClient.GetAsync(url);
            await EnsureSuccessAsync(resp);

            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<ReportItem>();

            Dictionary<string, ReportItem>? dict;
            try
            {
                dict = JsonSerializer.Deserialize<Dictionary<string, ReportItem>>(json, _jsonOptions);
            }
            catch
            {
                return new List<ReportItem>();
            }

            dict ??= new Dictionary<string, ReportItem>();

            var list = new List<ReportItem>(dict.Count);

            foreach (var kv in dict)
            {
                var it = kv.Value ?? new ReportItem();
                it.Id = string.IsNullOrWhiteSpace(it.Id) ? kv.Key : it.Id;

                // Se vier sem data (por algum motivo), evita ficar no topo
                if (it.CreatedAtUtcMs <= 0) it.CreatedAtUtcMs = 0;

                list.Add(it);
            }

            return list
                .OrderByDescending(r => r.CreatedAtUtcMs)
                .Take(limit)
                .ToList();
        }


        public async Task UpdateReportAdminDecisionAsync(string reportId, string status, string adminAction, string? adminNote = null)
        {
            if (string.IsNullOrWhiteSpace(reportId))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var rid = Uri.EscapeDataString(reportId);

            var patch = new
            {
                status = status ?? "open",
                adminAction = adminAction ?? "none",
                adminNote = adminNote ?? "",
                reviewedByAdminUid = FirebaseAuthService.Instance.CurrentUserUid ?? "",
                reviewedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await PatchAsync($"{BaseUrl}/reports/{rid}.json", patch, token);
        }

        // ============================================================
        // ADMIN: POSTS (listar / deletar)
        // ============================================================

        public async Task<IList<Post>> GetAllPostsAsync(int limit = 500)
        {
            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var url = WithAuth($"{BaseUrl}/posts.json", token);

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return new List<Post>();

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<Post>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, Post>>(json, _jsonOptions)
                       ?? new Dictionary<string, Post>();

            var list = new List<Post>(dict.Count);

            foreach (var kv in dict)
            {
                var p = kv.Value ?? new Post();
                p.Id = kv.Key;
                list.Add(p);
            }

            return list
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .ToList();
        }

        public async Task DeletePostAsync(string postId)
        {
            if (string.IsNullOrWhiteSpace(postId))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var pid = Uri.EscapeDataString(postId);

            var urlPost = WithAuth($"{BaseUrl}/posts/{pid}.json", token);
            var resp = await _httpClient.DeleteAsync(urlPost);
            await EnsureSuccessAsync(resp);

            var urlComments = WithAuth($"{BaseUrl}/comments/{pid}.json", token);
            await _httpClient.DeleteAsync(urlComments);

            var urlLikes = WithAuth($"{BaseUrl}/postLikes/{pid}.json", token);
            await _httpClient.DeleteAsync(urlLikes);
        }

        // ============================================================
        // ADMIN: USERS (listar / atualizar plano / boosts / deletar)
        // ============================================================

        private class PlanRecordLite
        {
            public string PlanType { get; set; } = "Free";
            public string Period { get; set; } = "monthly";
            public long StartedAtUtc { get; set; } = 0;
            public long ExpiresAtUtc { get; set; } = 0;
            public int BoostsAvailable { get; set; } = 0;
        }

        public async Task<IList<UserProfile>> GetAllUsersAsync(int limit = 1000)
        {
            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();

            var usersUrl = WithAuth($"{BaseUrl}/users.json", token);
            var response = await _httpClient.GetAsync(usersUrl);
            if (!response.IsSuccessStatusCode)
                return new List<UserProfile>();

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<UserProfile>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, UserProfile>>(json, _jsonOptions)
                       ?? new Dictionary<string, UserProfile>();

            // merge com /plans (para refletir UpgradePage/PlanService)
            var plans = await GetPlansLiteAsync(token);

            var list = new List<UserProfile>(dict.Count);

            foreach (var kv in dict)
            {
                var uid = kv.Key;
                var u = kv.Value ?? new UserProfile();
                u.Id = uid;

                if (plans.TryGetValue(uid, out var pr) && pr != null && !string.IsNullOrWhiteSpace(pr.PlanType))
                    u.Plan = pr.PlanType;

                list.Add(u);
            }

            return list.Take(limit).ToList();
        }

        private async Task<Dictionary<string, PlanRecordLite>> GetPlansLiteAsync(string? token)
        {
            try
            {
                var plansUrl = WithAuth($"{BaseUrl}/plans.json", token);
                var resp = await _httpClient.GetAsync(plansUrl);
                if (!resp.IsSuccessStatusCode)
                    return new Dictionary<string, PlanRecordLite>();

                var json = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return new Dictionary<string, PlanRecordLite>();

                return JsonSerializer.Deserialize<Dictionary<string, PlanRecordLite>>(json, _jsonOptions)
                       ?? new Dictionary<string, PlanRecordLite>();
            }
            catch
            {
                return new Dictionary<string, PlanRecordLite>();
            }
        }

        public async Task UpdateUserPlanAsync(string uid, string plan)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            plan = string.IsNullOrWhiteSpace(plan) ? "Free" : plan;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            // 1) compat: /users/{uid}/plan
            var urlUserPlan = WithAuth($"{BaseUrl}/users/{u}/plan.json", token);
            var contentUser = new StringContent($"\"{plan}\"", Encoding.UTF8, "application/json");
            var r1 = await _httpClient.PutAsync(urlUserPlan, contentUser);
            await EnsureSuccessAsync(r1);

            // 2) fonte do PlanService: /plans/{uid}
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            long expires = 0;
            if (plan.Equals("Plus", StringComparison.OrdinalIgnoreCase) ||
                plan.Equals("Premium", StringComparison.OrdinalIgnoreCase))
            {
                expires = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(); // mensal fictício
            }

            var patchPlans = new
            {
                planType = plan.Equals("Plus", StringComparison.OrdinalIgnoreCase) ? "Plus"
                         : plan.Equals("Premium", StringComparison.OrdinalIgnoreCase) ? "Premium"
                         : "Free",
                period = "monthly",
                startedAtUtc = expires > 0 ? now : 0,
                expiresAtUtc = expires
            };

            await PatchAsync($"{BaseUrl}/plans/{u}.json", patchPlans, token);
        }

        public async Task UpdateUserBoostTokensAsync(string uid, int extraTokens)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            // 1) compat: /users/{uid}/extraBoostTokens
            var urlUser = WithAuth($"{BaseUrl}/users/{u}/extraBoostTokens.json", token);
            var r1 = await _httpClient.PutAsync(urlUser, new StringContent($"{extraTokens}", Encoding.UTF8, "application/json"));
            await EnsureSuccessAsync(r1);

            // 2) fonte do PlanService: /plans/{uid}/boostsAvailable
            var urlPlans = WithAuth($"{BaseUrl}/plans/{u}/boostsAvailable.json", token);
            var r2 = await _httpClient.PutAsync(urlPlans, new StringContent($"{extraTokens}", Encoding.UTF8, "application/json"));
            await EnsureSuccessAsync(r2);
        }

        public async Task SetUserVerifiedAsync(string uid, bool isVerified, string verificationStatus)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            var urlV = WithAuth($"{BaseUrl}/users/{u}/isVerified.json", token);
            var urlS = WithAuth($"{BaseUrl}/users/{u}/verificationStatus.json", token);

            var c1 = new StringContent(isVerified ? "true" : "false", Encoding.UTF8, "application/json");
            var c2 = new StringContent($"\"{verificationStatus}\"", Encoding.UTF8, "application/json");

            var r1 = await _httpClient.PutAsync(urlV, c1);
            await EnsureSuccessAsync(r1);

            var r2 = await _httpClient.PutAsync(urlS, c2);
            await EnsureSuccessAsync(r2);

            if (isVerified)
            {
                var urlAt = WithAuth($"{BaseUrl}/users/{u}/verifiedAtUtcMs.json", token);
                var urlBy = WithAuth($"{BaseUrl}/users/{u}/verifiedByAdminUid.json", token);

                await _httpClient.PutAsync(urlAt, new StringContent($"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", Encoding.UTF8, "application/json"));
                await _httpClient.PutAsync(urlBy, new StringContent($"\"{FirebaseAuthService.Instance.CurrentUserUid ?? ""}\"", Encoding.UTF8, "application/json"));
            }
        }

        public async Task DeleteUserProfileAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            var url = WithAuth($"{BaseUrl}/users/{u}.json", token);
            var resp = await _httpClient.DeleteAsync(url);
            await EnsureSuccessAsync(resp);

            var urlVer = WithAuth($"{BaseUrl}/verifications/{u}.json", token);
            await _httpClient.DeleteAsync(urlVer);

            var urlPlans = WithAuth($"{BaseUrl}/plans/{u}.json", token);
            await _httpClient.DeleteAsync(urlPlans);
        }

        // ============================================================
        // VERIFICAÇÕES
        // /verifications/{uid}
        // ============================================================

        public async Task CreateOrUpdateVerificationAsync(VerificationRequest req)
        {
            if (req == null) throw new ArgumentNullException(nameof(req));
            if (string.IsNullOrWhiteSpace(req.UserId)) throw new ArgumentException("UserId obrigatório");

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var uid = Uri.EscapeDataString(req.UserId);

            var url = WithAuth($"{BaseUrl}/verifications/{uid}.json", token);

            var json = JsonSerializer.Serialize(req, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PutAsync(url, content);
            await EnsureSuccessAsync(resp);

            // espelha status no perfil para UI
            var urlStatus = WithAuth($"{BaseUrl}/users/{uid}/verificationStatus.json", token);
            var cStatus = new StringContent($"\"{req.Status}\"", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(urlStatus, cStatus);

            var urlSubmitted = WithAuth($"{BaseUrl}/users/{uid}/verificationSubmittedAtUtcMs.json", token);
            var cSub = new StringContent($"{req.CreatedAtUtcMs}", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(urlSubmitted, cSub);
        }

        public async Task<VerificationRequest?> GetVerificationAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return null;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            var url = WithAuth($"{BaseUrl}/verifications/{u}.json", token);

            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return null;

            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            return JsonSerializer.Deserialize<VerificationRequest>(json, _jsonOptions);
        }

        public async Task<IList<VerificationRequest>> GetAllVerificationsAsync(int limit = 1000)
        {
            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var url = WithAuth($"{BaseUrl}/verifications.json", token);

            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return new List<VerificationRequest>();

            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<VerificationRequest>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, VerificationRequest>>(json, _jsonOptions)
                       ?? new Dictionary<string, VerificationRequest>();

            var list = new List<VerificationRequest>(dict.Count);

            foreach (var kv in dict)
            {
                var v = kv.Value ?? new VerificationRequest();
                v.Id = kv.Key;
                v.UserId = string.IsNullOrWhiteSpace(v.UserId) ? kv.Key : v.UserId;
                list.Add(v);
            }

            return list
                .OrderByDescending(v => v.CreatedAtUtcMs)
                .Take(limit)
                .ToList();
        }

        public async Task UpdateVerificationStatusAsync(string uid, string status, string adminNote = "")
        {
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            var current = await GetVerificationAsync(uid);
            if (current == null)
                return;

            current.Status = status; // pending|approved|rejected
            current.ReviewedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            current.ReviewedByAdminUid = FirebaseAuthService.Instance.CurrentUserUid ?? "";
            current.AdminNote = adminNote ?? "";

            var url = WithAuth($"{BaseUrl}/verifications/{u}.json", token);
            var json = JsonSerializer.Serialize(current, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PutAsync(url, content);
            await EnsureSuccessAsync(resp);

            // espelha no user profile
            if (status == "approved")
                await SetUserVerifiedAsync(uid, true, "approved");
            else if (status == "rejected")
                await SetUserVerifiedAsync(uid, false, "rejected");
            else
                await SetUserVerifiedAsync(uid, false, "pending");
        }

        // ============================================================
        // SAC (SUPPORT)
        // /supportTickets/{ticketId}
        // ============================================================

        public async Task<string> CreateSupportTicketAsync(SupportTicket ticket)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var url = WithAuth($"{BaseUrl}/supportTickets.json", token);

            var json = JsonSerializer.Serialize(ticket, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PostAsync(url, content);
            await EnsureSuccessAsync(resp);

            var resultJson = await resp.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<FirebasePushResult>(resultJson, _jsonOptions);

            var id = result?.Name ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(id))
            {
                ticket.Id = id;

                var putUrl = WithAuth($"{BaseUrl}/supportTickets/{Uri.EscapeDataString(id)}.json", token);
                var putJson = JsonSerializer.Serialize(ticket, _jsonOptions);
                using var putContent = new StringContent(putJson, Encoding.UTF8, "application/json");

                await _httpClient.PutAsync(putUrl, putContent);
            }

            return id;
        }

        public async Task<IList<SupportTicket>> GetAllSupportTicketsAsync(int limit = 500)
        {
            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var url = WithAuth($"{BaseUrl}/supportTickets.json", token);

            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return new List<SupportTicket>();

            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<SupportTicket>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, SupportTicket>>(json, _jsonOptions)
                       ?? new Dictionary<string, SupportTicket>();

            var list = new List<SupportTicket>(dict.Count);

            foreach (var kv in dict)
            {
                var t = kv.Value ?? new SupportTicket();
                t.Id = kv.Key;
                list.Add(t);
            }

            return list
                .OrderByDescending(t => t.CreatedAtUtcMs)
                .Take(limit)
                .ToList();
        }

        public async Task UpdateSupportTicketStatusAsync(string ticketId, string status, string answer = "")
        {
            if (string.IsNullOrWhiteSpace(ticketId))
                return;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var tid = Uri.EscapeDataString(ticketId);

            var url = WithAuth($"{BaseUrl}/supportTickets/{tid}.json", token);

            var get = await _httpClient.GetAsync(url);
            await EnsureSuccessAsync(get);

            var json = await get.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return;

            var item = JsonSerializer.Deserialize<SupportTicket>(json, _jsonOptions);
            if (item == null) return;

            item.Status = status;
            item.Answer = answer ?? "";
            item.AnsweredAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            item.AnsweredByAdminUid = FirebaseAuthService.Instance.CurrentUserUid ?? "";

            var putJson = JsonSerializer.Serialize(item, _jsonOptions);
            using var content = new StringContent(putJson, Encoding.UTF8, "application/json");

            var put = await _httpClient.PutAsync(url, content);
            await EnsureSuccessAsync(put);
        }

        // ============================================================
        // SUSPENSÃO (ADMIN / LOGIN BLOCK)
        // /suspensions/{uid}
        // ============================================================

        private class SuspensionRecord
        {
            public bool IsSuspended { get; set; } = false;
            public long UntilUtcMs { get; set; } = 0; // 0 = indefinido
            public string Reason { get; set; } = "";
            public long UpdatedAtUtcMs { get; set; } = 0;
            public string UpdatedByUid { get; set; } = "";
        }

        /// <summary>
        /// Suspende um usuário por X dias (admin).
        /// Grava em /suspensions/{uid}.
        /// </summary>
        public async Task SuspendUserForDaysAsync(string uid, int days, string reason = "")
        {
            if (string.IsNullOrWhiteSpace(uid)) return;

            if (days < 0) days = 0;

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var until = days == 0
                ? 0 // 0 = indefinido
                : DateTimeOffset.UtcNow.AddDays(days).ToUnixTimeMilliseconds();

            var rec = new SuspensionRecord
            {
                IsSuspended = true,
                UntilUtcMs = until,
                Reason = reason ?? "",
                UpdatedAtUtcMs = now,
                UpdatedByUid = FirebaseAuthService.Instance.CurrentUserUid ?? ""
            };

            var url = WithAuth($"{BaseUrl}/suspensions/{u}.json", token);
            var json = JsonSerializer.Serialize(rec, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PutAsync(url, content);
            await EnsureSuccessAsync(resp);
        }

        /// <summary>
        /// Verifica se está suspenso e, se já expirou, remove a suspensão automaticamente.
        /// Retorna (isSuspended, untilUtcMs).
        /// </summary>
        public async Task<(bool isSuspended, long untilMs)> CheckAndAutoUnsuspendIfExpiredAsync(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
                return (false, 0);

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var u = Uri.EscapeDataString(uid);

            var url = WithAuth($"{BaseUrl}/suspensions/{u}.json", token);
            var resp = await _httpClient.GetAsync(url);

            if (!resp.IsSuccessStatusCode)
                return (false, 0);

            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return (false, 0);

            SuspensionRecord? rec;
            try
            {
                rec = JsonSerializer.Deserialize<SuspensionRecord>(json, _jsonOptions);
            }
            catch
            {
                return (false, 0);
            }

            if (rec == null || !rec.IsSuspended)
                return (false, 0);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // untilUtcMs == 0 => suspensão indefinida
            if (rec.UntilUtcMs > 0 && now >= rec.UntilUtcMs)
            {
                // expirou -> auto unsuspend
                var patch = new
                {
                    isSuspended = false,
                    updatedAtUtcMs = now,
                    updatedByUid = "auto"
                };

                await PatchAsync($"{BaseUrl}/suspensions/{u}.json", patch, token);
                return (false, rec.UntilUtcMs);
            }

            // ainda suspenso
            return (true, rec.UntilUtcMs);
        }


        // ============================================================
        // PUSH TOKENS (FCM)
        // ============================================================

        public sealed class PushTokenRecord
        {
            public string Token { get; set; } = string.Empty;
            public long UpdatedAtUtcMs { get; set; }
            public string Platform { get; set; } = string.Empty;
        }

        public async Task SavePushTokenAsync(string uid, string platform, string token)
        {
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(platform) || string.IsNullOrWhiteSpace(token))
                return;

            var idToken = await FirebaseAuthService.Instance.GetIdTokenAsync();
            if (string.IsNullOrWhiteSpace(idToken))
                return;

            var u = Uri.EscapeDataString(uid);
            var p = Uri.EscapeDataString(platform.ToLowerInvariant());

            var rec = new PushTokenRecord
            {
                Token = token,
                UpdatedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                Platform = platform.ToLowerInvariant()
            };

            // PUT é mais “determinístico” aqui (não depende de PATCH)
            var url = WithAuth($"{BaseUrl}/pushTokens/{u}/{p}.json", idToken);
            var json = JsonSerializer.Serialize(rec, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _httpClient.PutAsync(url, content);
            await EnsureSuccessAsync(resp);
        }

        public async Task RemovePushTokenAsync(string uid, string platform)
        {
            if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(platform))
                return;

            var idToken = await FirebaseAuthService.Instance.GetIdTokenAsync();
            if (string.IsNullOrWhiteSpace(idToken))
                return;

            var u = Uri.EscapeDataString(uid);
            var p = Uri.EscapeDataString(platform.ToLowerInvariant());

            var url = WithAuth($"{BaseUrl}/pushTokens/{u}/{p}.json", idToken);
            var resp = await _httpClient.DeleteAsync(url);
            await EnsureSuccessAsync(resp);
        }

        /// <summary>
        /// Lê tokens do RTDB em /pushTokens/{uid}
        /// Suporta:
        /// A) pushTokens/{uid}/{platform} = "TOKEN"
        /// B) pushTokens/{uid}/{platform}/{token} = true
        /// C) pushTokens/{uid}/{platform} = { token, updatedAtUtcMs, platform }
        /// </summary>
        public async Task<IList<PushTokenRecord>> GetPushTokensAsync(string uid)
        {
            var list = new List<PushTokenRecord>();

            if (string.IsNullOrWhiteSpace(uid))
                return list;

            var idToken = await FirebaseAuthService.Instance.GetIdTokenAsync();
            if (string.IsNullOrWhiteSpace(idToken))
                return list;

            var u = Uri.EscapeDataString(uid);
            var url = WithAuth($"{BaseUrl}/pushTokens/{u}.json", idToken);

            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return list;

            var json = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return list;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return list;

            foreach (var platformProp in doc.RootElement.EnumerateObject())
            {
                var platform = platformProp.Name;
                var pVal = platformProp.Value;

                // A) string token
                if (pVal.ValueKind == JsonValueKind.String)
                {
                    var tok = (pVal.GetString() ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(tok))
                    {
                        list.Add(new PushTokenRecord { Platform = platform, Token = tok, UpdatedAtUtcMs = 0 });
                    }
                    continue;
                }

                if (pVal.ValueKind != JsonValueKind.Object)
                    continue;

                // C) objeto com campo "token"
                if (pVal.TryGetProperty("token", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String)
                {
                    var tok = (tokenProp.GetString() ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(tok))
                    {
                        long updated = 0;
                        if (pVal.TryGetProperty("updatedAtUtcMs", out var up) && up.ValueKind == JsonValueKind.Number)
                            updated = up.GetInt64();

                        list.Add(new PushTokenRecord { Platform = platform, Token = tok, UpdatedAtUtcMs = updated });
                    }
                    continue;
                }

                // B) tokens como chaves
                foreach (var tokenKey in pVal.EnumerateObject())
                {
                    var key = (tokenKey.Name ?? "").Trim();
                    if (key.Length <= 20)
                        continue;

                    // token como chave => true/obj
                    list.Add(new PushTokenRecord
                    {
                        Platform = platform,
                        Token = key,
                        UpdatedAtUtcMs = 0
                    });
                }
            }

            // dedup
            var dedup = new Dictionary<string, PushTokenRecord>(StringComparer.Ordinal);
            foreach (var r in list)
            {
                if (string.IsNullOrWhiteSpace(r.Token)) continue;
                if (!dedup.ContainsKey(r.Token))
                    dedup[r.Token] = r;
            }

            return new List<PushTokenRecord>(dedup.Values);
        }

        /// <summary>
        /// Helper: retorna apenas strings (tokens), deduplicadas.
        /// </summary>
        public async Task<IList<string>> GetPushTokenStringsAsync(string uid)
        {
            var recs = await GetPushTokensAsync(uid);
            var set = new HashSet<string>(StringComparer.Ordinal);

            foreach (var r in recs)
                if (!string.IsNullOrWhiteSpace(r.Token))
                    set.Add(r.Token.Trim());

            return new List<string>(set);
        }



    }

}
