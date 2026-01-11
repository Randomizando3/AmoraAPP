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

        // =========================================================
        // Helpers (suporta /friendRequests = true OU {status, ts})
        // =========================================================

        private static bool IsActiveRequestNode(JsonElement node, bool onlyPending)
        {
            // formato antigo:
            //   true
            if (node.ValueKind == JsonValueKind.True) return true;
            if (node.ValueKind == JsonValueKind.False || node.ValueKind == JsonValueKind.Null) return false;

            // formato novo:
            //   { "status": "pending", "ts": 123 }
            if (node.ValueKind == JsonValueKind.Object)
            {
                string status = "";

                if (node.TryGetProperty("status", out var st))
                {
                    if (st.ValueKind == JsonValueKind.String)
                        status = (st.GetString() ?? "").Trim();
                }

                // Se não tem status, considera como "ativo" (compatibilidade)
                if (string.IsNullOrWhiteSpace(status))
                    return !onlyPending; // para lista de pendentes, exige status; para checks, aceita

                // Normaliza
                status = status.Trim().ToLowerInvariant();

                if (!onlyPending)
                {
                    // Para "existe request?"
                    // aceitamos qualquer status não-vazio, mas você pode restringir se quiser
                    return status != "rejected" && status != "canceled";
                }

                // Para listar incoming: só pendentes
                return status == "pending";
            }

            // formato estranho (string / number) -> tenta ser tolerante
            if (node.ValueKind == JsonValueKind.String)
            {
                var s = (node.GetString() ?? "").Trim().ToLowerInvariant();
                if (s == "true") return true;
                if (onlyPending) return s == "pending";
                return s != "" && s != "null" && s != "false";
            }

            return false;
        }

        private static bool TryParseJson(string json, out JsonDocument doc)
        {
            doc = null;
            try
            {
                doc = JsonDocument.Parse(json);
                return true;
            }
            catch
            {
                return false;
            }
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
        // OU = {status, ts}
        // =========================================

        /// <summary>
        /// Solicitações recebidas por mim (quem pediu é a chave).
        /// Lê:
        ///   /friendRequests/{meId}/{otherId} = true
        ///   OU /friendRequests/{meId}/{otherId} = { status, ts }
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

            // >>> Aqui é onde estava quebrando: Dictionary<string,bool>
            // Agora suportamos bool OU objeto.
            if (!TryParseJson(json, out var doc) || doc == null)
                return result;

            using (doc)
            {
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return result;

                foreach (var prop in root.EnumerateObject())
                {
                    var otherId = prop.Name;
                    var node = prop.Value;

                    // Lista apenas pendentes
                    if (IsActiveRequestNode(node, onlyPending: true))
                        result.Add(otherId);
                }
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
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return false;

            if (!TryParseJson(json, out var doc) || doc == null)
                return json.Contains("true", StringComparison.OrdinalIgnoreCase); // fallback

            using (doc)
            {
                return IsActiveRequestNode(doc.RootElement, onlyPending: false);
            }
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
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return false;

            if (!TryParseJson(json, out var doc) || doc == null)
                return json.Contains("true", StringComparison.OrdinalIgnoreCase); // fallback

            using (doc)
            {
                return IsActiveRequestNode(doc.RootElement, onlyPending: false);
            }
        }

        /// <summary>
        /// Cria uma nova solicitação: fromId → toId.
        /// Recomendo gravar no padrão do web:
        ///   /friendRequests/{toId}/{fromId} = { status:"pending", ts: unix }
        /// </summary>
        public async Task CreateFriendRequestAsync(string fromId, string toId)
        {
            if (string.IsNullOrWhiteSpace(fromId) || string.IsNullOrWhiteSpace(toId))
                return;

            if (fromId == toId)
                return;

            var path = $"/friendRequests/{toId}/{fromId}.json";
            var url = $"{BaseUrl}{path}";

            var payload = new
            {
                status = "pending",
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

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

            var url1 = $"{BaseUrl}/friendRequests/{meId}/{otherId}.json";
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
        // BLOQUEIO
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
