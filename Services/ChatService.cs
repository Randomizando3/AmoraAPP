using AmoraApp.Config;
using AmoraApp.Helpers;
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
    public class ChatService
    {
        public static ChatService Instance { get; } = new ChatService();

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        private string BaseUrl => FirebaseSettings.DatabaseUrl.TrimEnd('/');

        private ChatService()
        {
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        // Modelo interno do "header" do chat
        private class ChatHeader
        {
            public string ChatId { get; set; } = string.Empty;

            // 1x1
            public string User1Id { get; set; } = string.Empty;
            public string User2Id { get; set; } = string.Empty;

            // Grupo
            public bool IsGroup { get; set; } = false;
            public string GroupName { get; set; } = string.Empty;
            public Dictionary<string, bool>? Members { get; set; }

            public Dictionary<string, bool>? Favorites { get; set; }
            public Dictionary<string, bool>? Blocked { get; set; }

            public bool IsMatch { get; set; }

            public string LastMessageText { get; set; } = string.Empty;
            public long LastMessageAt { get; set; } // unix ms

            // >>> NOVO: foto do chat (grupo ou 1x1, mas usamos pra grupos)
            public string PhotoUrl { get; set; } = string.Empty;
        }

        private class FirebasePushResult
        {
            public string Name { get; set; } = string.Empty;
        }

        private static (string a, string b) OrderUsers(string u1, string u2)
        {
            if (string.CompareOrdinal(u1, u2) <= 0)
                return (u1, u2);
            return (u2, u1);
        }

        private string BuildChatId(string u1, string u2)
        {
            var (a, b) = OrderUsers(u1, u2);
            return $"{a}_{b}";
        }

        private async Task<ChatHeader?> GetChatHeaderAsync(string chatId)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return null;

            var url = $"{BaseUrl}/chats/{chatId}.json";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return null;

            return JsonSerializer.Deserialize<ChatHeader>(json, _jsonOptions);
        }

        // ============================================================
        // CHAT HEADER / CRIAÇÃO
        // ============================================================

        /// <summary>
        /// Chat 1x1.
        /// </summary>
        public async Task<string> GetOrCreateChatAsync(string user1Id, string user2Id)
        {
            if (string.IsNullOrWhiteSpace(user1Id) || string.IsNullOrWhiteSpace(user2Id))
                throw new ArgumentException("user1Id e user2Id são obrigatórios.");

            var chatId = BuildChatId(user1Id, user2Id);
            var url = $"{BaseUrl}/chats/{chatId}.json";

            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(json) || json == "null")
            {
                var (a, b) = OrderUsers(user1Id, user2Id);

                var header = new ChatHeader
                {
                    ChatId = chatId,
                    User1Id = a,
                    User2Id = b,
                    IsGroup = false,
                    IsMatch = false,
                    LastMessageText = string.Empty,
                    LastMessageAt = 0,
                    Members = new Dictionary<string, bool>
                    {
                        [a] = true,
                        [b] = true
                    },
                    PhotoUrl = string.Empty
                };

                var body = JsonSerializer.Serialize(header, _jsonOptions);
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                await _httpClient.PutAsync(url, content);
            }

            return chatId;
        }

        /// <summary>
        /// Cria um chat de grupo com até 8 membros (incluindo o criador).
        /// </summary>
        public async Task<string> CreateGroupChatAsync(string creatorId, string groupName, IEnumerable<string> memberIds)
        {
            if (string.IsNullOrWhiteSpace(creatorId))
                throw new ArgumentException("creatorId é obrigatório.");

            if (string.IsNullOrWhiteSpace(groupName))
                throw new ArgumentException("groupName é obrigatório.");

            var members = new HashSet<string>(memberIds ?? Array.Empty<string>());

            members.Add(creatorId); // garante criador
            members.RemoveWhere(string.IsNullOrWhiteSpace);

            if (members.Count < 2)
                throw new ArgumentException("Um grupo precisa ter pelo menos 2 membros.");

            if (members.Count > 8)
                throw new ArgumentException("Um grupo pode ter no máximo 8 membros.");

            var chatId = $"group_{Guid.NewGuid():N}";
            var url = $"{BaseUrl}/chats/{chatId}.json";

            // Define User1/User2 só para compat 1x1 (aqui pega dois quaisquer)
            var list = members.ToList();
            var user1 = list[0];
            var user2 = list.Count > 1 ? list[1] : list[0];

            var header = new ChatHeader
            {
                ChatId = chatId,
                IsGroup = true,
                GroupName = groupName,
                User1Id = user1,
                User2Id = user2,
                Members = members.ToDictionary(m => m, m => true),
                IsMatch = false,
                LastMessageText = string.Empty,
                LastMessageAt = 0,
                PhotoUrl = string.Empty // será preenchido depois (foto manual ou auto)
            };

            var body = JsonSerializer.Serialize(header, _jsonOptions);
            var content = new StringContent(body, Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(url, content);

            return chatId;
        }

        // ============================================================
        // GRUPOS - MEMBROS
        // ============================================================

        public async Task<IList<string>> GetGroupMemberIdsAsync(string chatId)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return new List<string>();

            var header = await GetChatHeaderAsync(chatId);
            if (header == null)
                return new List<string>();

            if (header.Members != null && header.Members.Count > 0)
                return header.Members.Keys.ToList();

            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(header.User1Id))
                list.Add(header.User1Id);
            if (!string.IsNullOrWhiteSpace(header.User2Id) && header.User2Id != header.User1Id)
                list.Add(header.User2Id);

            return list;
        }

        public async Task LeaveGroupAsync(string chatId, string userId)
        {
            if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(userId))
                return;

            var memberPath = $"{BaseUrl}/chats/{chatId}/members/{userId}.json";
            await _httpClient.DeleteAsync(memberPath);

            var hiddenPath = $"{BaseUrl}/chatHidden/{userId}/{chatId}.json";
            var content = new StringContent("true", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(hiddenPath, content);
        }

        public async Task AddGroupMembersAsync(string chatId, string requesterId, IEnumerable<string> newMemberIds)
        {
            if (string.IsNullOrWhiteSpace(chatId) || newMemberIds == null)
                return;

            var header = await GetChatHeaderAsync(chatId);
            if (header == null || !header.IsGroup)
                return;

            foreach (var id in newMemberIds)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var path = $"{BaseUrl}/chats/{chatId}/members/{id}.json";
                var content = new StringContent("true", Encoding.UTF8, "application/json");
                await _httpClient.PutAsync(path, content);
            }
        }

        public async Task RemoveGroupMemberAsync(string chatId, string requesterId, string memberId)
        {
            if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(memberId))
                return;

            var header = await GetChatHeaderAsync(chatId);
            if (header == null || !header.IsGroup)
                return;

            var path = $"{BaseUrl}/chats/{chatId}/members/{memberId}.json";
            await _httpClient.DeleteAsync(path);
        }

        public async Task DeleteGroupAsync(string chatId, string requesterId)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return;

            var header = await GetChatHeaderAsync(chatId);
            if (header == null || !header.IsGroup)
                return;

            var chatPath = $"{BaseUrl}/chats/{chatId}.json";
            await _httpClient.DeleteAsync(chatPath);

            var msgPath = $"{BaseUrl}/chatMessages/{chatId}.json";
            await _httpClient.DeleteAsync(msgPath);
        }

        // Helpers simples usados em UI
        public async Task AddMemberToGroupAsync(string chatId, string memberId)
        {
            var requester = FirebaseAuthService.Instance.CurrentUserUid ?? string.Empty;
            await AddGroupMembersAsync(chatId, requester, new[] { memberId });
        }

        public async Task RemoveMemberFromGroupAsync(string chatId, string memberId)
        {
            var requester = FirebaseAuthService.Instance.CurrentUserUid ?? string.Empty;
            await RemoveGroupMemberAsync(chatId, requester, memberId);
        }

        public async Task DeleteGroupAsync(string chatId)
        {
            var requester = FirebaseAuthService.Instance.CurrentUserUid ?? string.Empty;
            await DeleteGroupAsync(chatId, requester);
        }

        // ============================================================
        // MENSAGENS
        // ============================================================

        public async Task<IList<ChatMessage>> GetMessagesAsync(string chatId, int limit = 80)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                return new List<ChatMessage>();

            var url = $"{BaseUrl}/chatMessages/{chatId}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return new List<ChatMessage>();

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return new List<ChatMessage>();

            var dict = JsonSerializer.Deserialize<Dictionary<string, ChatMessage>>(json, _jsonOptions)
                       ?? new Dictionary<string, ChatMessage>();

            var list = dict
                .Select(kv =>
                {
                    kv.Value.Id = kv.Key;
                    kv.Value.ChatId = chatId;
                    return kv.Value;
                })
                .OrderBy(m => m.CreatedAt)
                .TakeLast(limit)
                .ToList();

            return list;
        }

        public async Task SendMessageAsync(string chatId, ChatMessage message)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                throw new ArgumentException("chatId é obrigatório.");

            if (message == null
                || string.IsNullOrWhiteSpace(message.SenderId)
                || (string.IsNullOrWhiteSpace(message.Text)
                    && string.IsNullOrWhiteSpace(message.ImageBase64)
                    && string.IsNullOrWhiteSpace(message.AudioUrl)))
                throw new ArgumentException("Mensagem inválida.");

            message.ChatId = chatId;

            if (message.CreatedAt == 0)
                message.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            message.ReadBy ??= new Dictionary<string, bool>();
            if (!string.IsNullOrWhiteSpace(message.SenderId))
                message.ReadBy[message.SenderId] = true;

            var url = $"{BaseUrl}/chatMessages/{chatId}.json";
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync();
            var pushResult = JsonSerializer.Deserialize<FirebasePushResult>(resultJson, _jsonOptions);
            var newId = pushResult?.Name ?? string.Empty;

            var lastPreview =
                !string.IsNullOrWhiteSpace(message.Text)
                    ? message.Text
                    : (!string.IsNullOrWhiteSpace(message.ImageBase64)
                        ? "[Imagem]"
                        : (!string.IsNullOrWhiteSpace(message.AudioUrl)
                            ? "[Áudio]"
                            : string.Empty));

            var headerUrl = $"{BaseUrl}/chats/{chatId}.json";
            var patch = new
            {
                lastMessageText = lastPreview,
                lastMessageAt = message.CreatedAt
            };
            var patchJson = JsonSerializer.Serialize(patch, _jsonOptions);
            var patchContent = new StringContent(patchJson, Encoding.UTF8, "application/json");
            await _httpClient.PatchAsync(headerUrl, patchContent);

            var header = await GetChatHeaderAsync(chatId);
            if (header != null && !string.IsNullOrWhiteSpace(message.SenderId))
            {
                var senderId = message.SenderId;

                var members = header.Members?.Keys?.ToList();
                if (members == null || members.Count == 0)
                {
                    members = new List<string>();
                    if (!string.IsNullOrWhiteSpace(header.User1Id))
                        members.Add(header.User1Id);
                    if (!string.IsNullOrWhiteSpace(header.User2Id) && header.User2Id != header.User1Id)
                        members.Add(header.User2Id);
                }

                foreach (var userId in members.Where(m => !string.IsNullOrWhiteSpace(m)))
                {
                    var metaPath = $"{BaseUrl}/chatMeta/{chatId}/{userId}/unreadCount.json";

                    if (userId == senderId)
                    {
                        await _httpClient.PutAsync(
                            metaPath,
                            new StringContent("0", Encoding.UTF8, "application/json"));
                    }
                    else
                    {
                        var unreadResp = await _httpClient.GetAsync(metaPath);
                        var unreadJson = await unreadResp.Content.ReadAsStringAsync();

                        int currentUnread = 0;
                        if (!string.IsNullOrWhiteSpace(unreadJson) && unreadJson != "null")
                            int.TryParse(unreadJson, out currentUnread);

                        currentUnread++;
                        await _httpClient.PutAsync(
                            metaPath,
                            new StringContent(currentUnread.ToString(), Encoding.UTF8, "application/json"));
                    }
                }
            }
        }

        public async Task MarkMessagesAsReadAsync(string chatId, string userId, IEnumerable<ChatMessage> messages)
        {
            if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(userId))
                return;

            if (messages == null)
                return;

            foreach (var msg in messages)
            {
                if (msg == null || string.IsNullOrWhiteSpace(msg.Id))
                    continue;

                if (msg.ReadBy != null && msg.ReadBy.TryGetValue(userId, out var already) && already)
                    continue;

                var path = $"{BaseUrl}/chatMessages/{chatId}/{msg.Id}/readBy/{userId}.json";
                var content = new StringContent("true", Encoding.UTF8, "application/json");
                await _httpClient.PutAsync(path, content);
            }

            var metaPath = $"{BaseUrl}/chatMeta/{chatId}/{userId}/unreadCount.json";
            await _httpClient.PutAsync(metaPath, new StringContent("0", Encoding.UTF8, "application/json"));
        }

        // ============================================================
        // LISTA DE CHATS DO USUÁRIO
        // ============================================================

        public async Task<IList<ChatItem>> GetChatsForUserAsync(string userId)
        {
            var result = new List<ChatItem>();

            if (string.IsNullOrWhiteSpace(userId))
                return result;

            var url = $"{BaseUrl}/chats.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return result;

            var json = await response.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return result;

            var dict = JsonSerializer.Deserialize<Dictionary<string, ChatHeader>>(json, _jsonOptions)
                       ?? new Dictionary<string, ChatHeader>();

            foreach (var kv in dict)
            {
                var header = kv.Value;
                if (header == null)
                    continue;

                var hiddenUrl = $"{BaseUrl}/chatHidden/{userId}/{header.ChatId}.json";
                var hiddenResponse = await _httpClient.GetAsync(hiddenUrl);
                var hiddenJson = await hiddenResponse.Content.ReadAsStringAsync();
                if (hiddenResponse.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(hiddenJson) && hiddenJson != "null")
                {
                    bool isHidden = false;
                    bool.TryParse(hiddenJson, out isHidden);
                    if (isHidden)
                        continue;
                }

                bool participates = false;

                if (!header.IsGroup)
                {
                    participates = header.User1Id == userId || header.User2Id == userId;
                }
                else
                {
                    participates =
                        (header.Members != null && header.Members.ContainsKey(userId)) ||
                        header.User1Id == userId || header.User2Id == userId;
                }

                if (!participates)
                    continue;

                bool isGroup = header.IsGroup;
                string displayName;
                string otherId = string.Empty;
                string photoUrl = string.Empty;

                int membersCount = 0;
                if (header.Members != null)
                {
                    membersCount = header.Members.Keys.Count;
                }
                else
                {
                    var set = new HashSet<string>();
                    if (!string.IsNullOrWhiteSpace(header.User1Id))
                        set.Add(header.User1Id);
                    if (!string.IsNullOrWhiteSpace(header.User2Id))
                        set.Add(header.User2Id);
                    membersCount = set.Count;
                }

                bool isGroupAdmin = header.IsGroup && header.User1Id == userId;

                if (isGroup)
                {
                    displayName = string.IsNullOrWhiteSpace(header.GroupName)
                        ? "Grupo"
                        : $"Grupo {header.GroupName}";
                    otherId = string.Empty;

                    // 1) Já tem foto salva no header? usa.
                    if (!string.IsNullOrWhiteSpace(header.PhotoUrl))
                    {
                        photoUrl = header.PhotoUrl;
                    }
                    else
                    {
                        // 2) Grupo antigo sem foto -> gera UMA vez aleatório e salva
                        var auto = AvatarGenerator.GetGroupAvatarUrl(header.GroupName, header.ChatId);
                        photoUrl = auto;

                        try
                        {
                            await FirebaseDatabaseService.Instance.UpdateChatPhotoAsync(header.ChatId, auto);
                        }
                        catch
                        {
                            // se der erro, só segue com auto local mesmo
                        }
                    }
                }
                else
                {
                    otherId = header.User1Id == userId ? header.User2Id : header.User1Id;

                    var otherProfile = await FirebaseDatabaseService.Instance.GetUserProfileAsync(otherId);
                    displayName = otherProfile?.DisplayName ?? "Usuário";
                    photoUrl = otherProfile?.PhotoUrl ?? string.Empty;
                }

                var isFavorite = header.Favorites != null
                                 && header.Favorites.TryGetValue(userId, out var fav)
                                 && fav;

                int unreadCount = 0;
                var unreadPath = $"{BaseUrl}/chatMeta/{header.ChatId}/{userId}/unreadCount.json";
                var unreadResp = await _httpClient.GetAsync(unreadPath);
                var unreadJson = await unreadResp.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(unreadJson) && unreadJson != "null")
                    int.TryParse(unreadJson, out unreadCount);

                var item = new ChatItem
                {
                    ChatId = header.ChatId,
                    UserId = otherId,
                    DisplayName = displayName,
                    PhotoUrl = photoUrl,
                    IsMatch = header.IsMatch,
                    IsFavorite = isFavorite,
                    LastMessagePreview = header.LastMessageText ?? string.Empty,
                    LastMessageAt = header.LastMessageAt > 0
                        ? DateTimeOffset.FromUnixTimeMilliseconds(header.LastMessageAt).LocalDateTime
                        : DateTime.MinValue,
                    UnreadCount = unreadCount,
                    IsGroup = isGroup,
                    GroupName = header.GroupName ?? string.Empty,
                    MembersCount = membersCount,
                    IsGroupAdmin = isGroupAdmin
                };

                result.Add(item);
            }

            return result
                .OrderByDescending(c => c.IsFavorite)
                .ThenByDescending(c => c.IsMatch)
                .ThenByDescending(c => c.LastMessageAt)
                .ToList();
        }

        // ============================================================
        // FAVORITO / BLOQUEAR / EXCLUIR (soft-delete)
        // ============================================================

        public async Task SetFavoriteAsync(string chatId, string userId, bool isFavorite)
        {
            if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(userId))
                return;

            var path = $"{BaseUrl}/chats/{chatId}/favorites/{userId}.json";

            if (!isFavorite)
            {
                await _httpClient.DeleteAsync(path);
                return;
            }

            var content = new StringContent("true", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(path, content);
        }

        public async Task BlockChatAsync(string chatId, string userId)
        {
            if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(userId))
                return;

            var path = $"{BaseUrl}/chats/{chatId}/blocked/{userId}.json";
            var content = new StringContent("true", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(path, content);
        }

        public async Task DeleteChatForUserAsync(string chatId, string userId)
        {
            if (string.IsNullOrWhiteSpace(chatId) || string.IsNullOrWhiteSpace(userId))
                return;

            var path = $"{BaseUrl}/chatHidden/{userId}/{chatId}.json";
            var content = new StringContent("true", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(path, content);
        }
    }
}
