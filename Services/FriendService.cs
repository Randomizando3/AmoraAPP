using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AmoraApp.Config;

namespace AmoraApp.Services
{
    public class FriendService
    {
        public static FriendService Instance { get; } = new FriendService();

        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        private string BaseUrl => FirebaseSettings.DatabaseUrl.TrimEnd('/');

        private FriendService()
        {
            _httpClient = new HttpClient();
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        // =========================================
        // AMIGOS
        // /friends/{uid}/{friendId} = true
        // =========================================

        public async Task AddFriendAsync(string meId, string friendId)
        {
            if (string.IsNullOrWhiteSpace(meId) || string.IsNullOrWhiteSpace(friendId))
                return;

            if (meId == friendId)
                return;

            // grava nos dois lados
            var path1 = $"/friends/{meId}/{friendId}.json";
            var path2 = $"/friends/{friendId}/{meId}.json";

            var url1 = $"{BaseUrl}{path1}";
            var url2 = $"{BaseUrl}{path2}";

            var content = new StringContent("true", Encoding.UTF8, "application/json");

            var resp1 = await _httpClient.PutAsync(url1, content);
            resp1.EnsureSuccessStatusCode();

            // novo content (o anterior já foi consumido)
            content = new StringContent("true", Encoding.UTF8, "application/json");
            var resp2 = await _httpClient.PutAsync(url2, content);
            resp2.EnsureSuccessStatusCode();
        }

        public async Task RemoveFriendAsync(string meId, string friendId)
        {
            if (string.IsNullOrWhiteSpace(meId) || string.IsNullOrWhiteSpace(friendId))
                return;

            var url1 = $"{BaseUrl}/friends/{meId}/{friendId}.json";
            var url2 = $"{BaseUrl}/friends/{friendId}/{meId}.json";

            await _httpClient.DeleteAsync(url1);
            await _httpClient.DeleteAsync(url2);
        }

        public async Task<bool> AreFriendsAsync(string userA, string userB)
        {
            if (string.IsNullOrWhiteSpace(userA) || string.IsNullOrWhiteSpace(userB))
                return false;

            var url = $"{BaseUrl}/friends/{userA}/{userB}.json";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            return !string.IsNullOrWhiteSpace(json) && json != "null" && json.Contains("true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<string>> GetFriendsAsync(string meId)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(meId))
                return result;

            var url = $"{BaseUrl}/friends/{meId}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return result;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return result;

            var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json, _jsonOptions)
                       ?? new Dictionary<string, bool>();

            foreach (var kv in dict)
            {
                if (kv.Value)
                    result.Add(kv.Key);
            }

            return result;
        }

        // =========================================
        // SOLICITAÇÕES DE AMIZADE
        // /friendRequests/{targetUserId}/{fromUserId} = true
        // =========================================

        /// <summary>
        /// Solicitações recebidas por mim (quem pediu é a chave).
        /// /friendRequests/{meId}/{otherId} = true
        /// </summary>
        public async Task<List<string>> GetIncomingRequestsAsync(string meId)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(meId))
                return result;

            var url = $"{BaseUrl}/friendRequests/{meId}.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return result;

            var json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return result;

            var dict = JsonSerializer.Deserialize<Dictionary<string, bool>>(json, _jsonOptions)
                       ?? new Dictionary<string, bool>();

            foreach (var kv in dict)
            {
                if (kv.Value)
                    result.Add(kv.Key);
            }

            return result;
        }

        /// <summary>
        /// Há uma solicitação enviada POR otherId PARA mim.
        /// </summary>
        public async Task<bool> HasIncomingRequestAsync(string meId, string otherId)
        {
            if (string.IsNullOrWhiteSpace(meId) || string.IsNullOrWhiteSpace(otherId))
                return false;

            var url = $"{BaseUrl}/friendRequests/{meId}/{otherId}.json";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            return !string.IsNullOrWhiteSpace(json) && json != "null" && json.Contains("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Eu já enviei solicitação PARA otherId?
        /// (fica em /friendRequests/{otherId}/{meId})
        /// </summary>
        public async Task<bool> HasOutgoingRequestAsync(string meId, string otherId)
        {
            if (string.IsNullOrWhiteSpace(meId) || string.IsNullOrWhiteSpace(otherId))
                return false;

            var url = $"{BaseUrl}/friendRequests/{otherId}/{meId}.json";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadAsStringAsync();
            return !string.IsNullOrWhiteSpace(json) && json != "null" && json.Contains("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Cria uma nova solicitação: fromId → toId.
        /// /friendRequests/{toId}/{fromId} = true
        /// </summary>
        public async Task CreateFriendRequestAsync(string fromId, string toId)
        {
            if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
                return;

            if (fromId == toId)
                return;

            var path = $"/friendRequests/{toId}/{fromId}.json";
            var url = $"{BaseUrl}{path}";

            var content = new StringContent("true", Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Aceita a amizade entre meId e otherId:
        /// - adiciona ambos em /friends
        /// - remove solicitações pendentes em ambos sentidos
        /// </summary>
        public async Task AcceptFriendshipAsync(string meId, string otherId)
        {
            if (string.IsNullOrWhiteSpace(meId) || string.IsNullOrWhiteSpace(otherId))
                return;

            await AddFriendAsync(meId, otherId);

            // remove request other → me
            var url1 = $"{BaseUrl}/friendRequests/{meId}/{otherId}.json";
            // remove request me → other (se existir)
            var url2 = $"{BaseUrl}/friendRequests/{otherId}/{meId}.json";

            await _httpClient.DeleteAsync(url1);
            await _httpClient.DeleteAsync(url2);
        }

        /// <summary>
        /// Recusa uma solicitação (apenas remove /friendRequests/{meId}/{otherId}).
        /// </summary>
        public async Task RejectFriendRequestAsync(string meId, string otherId)
        {
            if (string.IsNullOrWhiteSpace(meId) || string.IsNullOrWhiteSpace(otherId))
                return;

            var url = $"{BaseUrl}/friendRequests/{meId}/{otherId}.json";
            await _httpClient.DeleteAsync(url);
        }


        // =========================================
        // BLOQUEIO (bem simples, por enquanto)
        // /blocked/{meId}/{otherId} = true
        // =========================================

        public async Task CreateBlockAsync(string meId, string otherId)
        {
            if (string.IsNullOrWhiteSpace(meId) || string.IsNullOrWhiteSpace(otherId))
                return;

            var path = $"/blocked/{meId}/{otherId}.json";
            var url = $"{BaseUrl}{path}";

            var content = new StringContent("true", Encoding.UTF8, "application/json");
            await _httpClient.PutAsync(url, content);
        }

    }
}
