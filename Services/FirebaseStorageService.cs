using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using AmoraApp.Config;

namespace AmoraApp.Services
{
    public class FirebaseStorageService
    {
        public static FirebaseStorageService Instance { get; } = new();

        private readonly HttpClient _http = new HttpClient();

        private FirebaseStorageService() { }

        /// <summary>
        /// Upload genérico de arquivo para o Firebase Storage e retorna uma URL.
        /// Observação: com regras "read: if true", a URL sem token funciona.
        /// </summary>
        public async Task<string?> UploadFileAsync(Stream fileStream, string fileName, string contentType = "application/octet-stream")
        {
            if (fileStream == null)
                throw new ArgumentNullException(nameof(fileStream));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            var bucket = FirebaseSettings.StorageBucket;
            if (string.IsNullOrWhiteSpace(bucket))
                throw new InvalidOperationException("FirebaseSettings.StorageBucket não está configurado.");

            // Lê para buffer (precisamos poder tentar mais de 1 método/URL)
            byte[] data;
            using (var ms = new MemoryStream())
            {
                await fileStream.CopyToAsync(ms);
                data = ms.ToArray();
            }

            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();
            var encodedName = Uri.EscapeDataString(fileName);

            // Endpoint v0 (Firebase Storage REST)
            var baseEndpoint = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o";
            var urlMedia = $"{baseEndpoint}?uploadType=media&name={encodedName}";
            var urlNameOnly = $"{baseEndpoint}?name={encodedName}";

            // Tenta combinações comuns para evitar "Invalid HTTP method/URL pair"
            HttpResponseMessage resp;

            resp = await TryUploadAsync(urlMedia, HttpMethod.Post, data, contentType, token);
            if (IsInvalidMethodUrlPair(resp))
                resp = await TryUploadAsync(urlMedia, HttpMethod.Put, data, contentType, token);

            if (IsInvalidMethodUrlPair(resp))
                resp = await TryUploadAsync(urlNameOnly, HttpMethod.Post, data, contentType, token);

            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Erro ao enviar para Firebase Storage. Status: {(int)resp.StatusCode} - {resp.ReasonPhrase}\nResposta: {json}");
            }

            // Resposta típica contém:
            // { "name": "...", "bucket": "...", "downloadTokens": "..." }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var storedName = root.TryGetProperty("name", out var nameProp)
                ? (nameProp.GetString() ?? fileName)
                : fileName;

            // Se suas regras permitem read público (como no conjunto que te passei),
            // a URL abaixo funciona sem token.
            var publicUrl =
                $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{Uri.EscapeDataString(storedName)}?alt=media";

            // Se vier downloadTokens, também montamos a URL com token (funciona mesmo com regras fechadas)
            if (root.TryGetProperty("downloadTokens", out var tokenProp))
            {
                var dl = tokenProp.GetString();
                if (!string.IsNullOrWhiteSpace(dl))
                {
                    // Pode vir com vários tokens separados por vírgula
                    var first = dl.Split(',')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(first))
                        return publicUrl + $"&token={first}";
                }
            }

            return publicUrl;
        }

        private async Task<HttpResponseMessage> TryUploadAsync(
            string url,
            HttpMethod method,
            byte[] data,
            string contentType,
            string? idToken)
        {
            using var req = new HttpRequestMessage(method, url);

            // auth do Storage é via header, NÃO via ?auth=
            if (!string.IsNullOrWhiteSpace(idToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            req.Content = content;

            return await _http.SendAsync(req);
        }

        private static bool IsInvalidMethodUrlPair(HttpResponseMessage resp)
        {
            if (resp.StatusCode != HttpStatusCode.BadRequest)
                return false;

            // Lemos o body fora? aqui não. Só flag para retry básico.
            return true;
        }

        /// <summary>
        /// Mantido para compatibilidade: upload de imagem (usa image/jpeg).
        /// </summary>
        public Task<string?> UploadImageAsync(Stream fileStream, string fileName)
            => UploadFileAsync(fileStream, fileName, "image/jpeg");
    }
}
