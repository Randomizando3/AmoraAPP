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
            public string User1Id { get; set; } = string.Empty;
            public string User2Id { get; set; } = string.Empty;

            public Dictionary<string, bool>? Favorites { get; set; }
            public Dictionary<string, bool>? Blocked { get; set; }

            public bool IsMatch { get; set; }

            public string LastMessageText { get; set; } = string.Empty;
            public long LastMessageAt { get; set; } // unix ms
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

        // ============================================================
        // CHAT HEADER / CRIAÇÃO
        // ============================================================

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
                    IsMatch = false,
                    LastMessageText = string.Empty,
                    LastMessageAt = 0
                };

                var body = JsonSerializer.Serialize(header, _jsonOptions);
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                await _httpClient.PutAsync(url, content);
            }

            return chatId;
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

            // Agora aceita: texto OU imagem
            if (message == null
                || string.IsNullOrWhiteSpace(message.SenderId)
                || (string.IsNullOrWhiteSpace(message.Text) && string.IsNullOrWhiteSpace(message.ImageBase64)))
                throw new ArgumentException("Mensagem inválida.");

            message.ChatId = chatId;

            if (message.CreatedAt == 0)
                message.CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Garante que o remetente já conta como "leu"
            message.ReadBy ??= new Dictionary<string, bool>();
            if (!string.IsNullOrWhiteSpace(message.SenderId))
                message.ReadBy[message.SenderId] = true;

            // Envia mensagem
            var url = $"{BaseUrl}/chatMessages/{chatId}.json";
            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync();
            var pushResult = JsonSerializer.Deserialize<FirebasePushResult>(resultJson, _jsonOptions);
            var newId = pushResult?.Name ?? string.Empty;

            // Texto de preview para lista (se não tiver texto, usa marcador de imagem)
            var lastPreview = !string.IsNullOrWhiteSpace(message.Text)
                ? message.Text
                : (!string.IsNullOrWhiteSpace(message.ImageBase64) ? "[Imagem]" : string.Empty);

            // Atualiza header com última mensagem
            var headerUrl = $"{BaseUrl}/chats/{chatId}.json";
            var patch = new
            {
                lastMessageText = lastPreview,
                lastMessageAt = message.CreatedAt
            };
            var patchJson = JsonSerializer.Serialize(patch, _jsonOptions);
            var patchContent = new StringContent(patchJson, Encoding.UTF8, "application/json");
            await _httpClient.PatchAsync(headerUrl, patchContent);

            // ==== unread count (igual estava) ====
            var header = await GetChatHeaderAsync(chatId);
            if (header != null && !string.IsNullOrWhiteSpace(message.SenderId))
            {
                var senderId = message.SenderId;
                var otherId = header.User1Id == senderId ? header.User2Id : header.User1Id;

                if (!string.IsNullOrWhiteSpace(otherId))
                {
                    // Zera unread do remetente
                    var mePath = $"{BaseUrl}/chatMeta/{chatId}/{senderId}/unreadCount.json";
                    await _httpClient.PutAsync(mePath, new StringContent("0", Encoding.UTF8, "application/json"));

                    // Incrementa unread do outro
                    var otherPath = $"{BaseUrl}/chatMeta/{chatId}/{otherId}/unreadCount.json";
                    var unreadResp = await _httpClient.GetAsync(otherPath);
                    var unreadJson = await unreadResp.Content.ReadAsStringAsync();

                    int currentUnread = 0;
                    if (!string.IsNullOrWhiteSpace(unreadJson) && unreadJson != "null")
                        int.TryParse(unreadJson, out currentUnread);

                    currentUnread++;
                    await _httpClient.PutAsync(otherPath,
                        new StringContent(currentUnread.ToString(), Encoding.UTF8, "application/json"));
                }
            }
        }


        /// <summary>
        /// Marca mensagens como lidas (readBy[userId] = true) e zera unreadCount para esse usuário.
        /// </summary>
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

                // Se já marcado, ignora
                if (msg.ReadBy != null && msg.ReadBy.TryGetValue(userId, out var already) && already)
                    continue;

                var path = $"{BaseUrl}/chatMessages/{chatId}/{msg.Id}/readBy/{userId}.json";
                var content = new StringContent("true", Encoding.UTF8, "application/json");
                await _httpClient.PutAsync(path, content);
            }

            // Zera unreadCount
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

            // /chats.json
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

                // "Apagado" para esse usuário?
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

                bool participates = header.User1Id == userId || header.User2Id == userId;
                if (!participates)
                    continue;

                var otherId = header.User1Id == userId ? header.User2Id : header.User1Id;

                // Pega perfil do outro
                var otherProfile = await FirebaseDatabaseService.Instance.GetUserProfileAsync(otherId);
                var displayName = otherProfile?.DisplayName ?? "Usuário";
                var photoUrl = otherProfile?.PhotoUrl ?? string.Empty;

                var isFavorite = header.Favorites != null
                                 && header.Favorites.TryGetValue(userId, out var fav)
                                 && fav;

                // UnreadCount de chatMeta
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
                    UnreadCount = unreadCount
                };

                result.Add(item);
            }

            // ordenação por favoritos, matches, horário
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
                // remove
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
