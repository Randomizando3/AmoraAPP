using System;
using System.IO;
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
        /// Upload genérico de arquivo para o Firebase Storage e retorna a URL pública.
        /// </summary>
        /// <param name="fileStream">Stream do arquivo</param>
        /// <param name="fileName">Caminho/nome do arquivo dentro do bucket</param>
        /// <param name="contentType">MIME type (ex: image/jpeg, audio/mpeg, application/octet-stream)</param>
        public async Task<string?> UploadFileAsync(Stream fileStream, string fileName, string contentType = "application/octet-stream")
        {
            if (fileStream == null)
                throw new ArgumentNullException(nameof(fileStream));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentNullException(nameof(fileName));

            // Bucket configurado no FirebaseSettings (ex: "seu-projeto.appspot.com")
            var bucket = FirebaseSettings.StorageBucket;
            if (string.IsNullOrWhiteSpace(bucket))
                throw new InvalidOperationException("FirebaseSettings.StorageBucket não está configurado.");

            // Token opcional (se regras exigirem auth)
            var token = await FirebaseAuthService.Instance.GetIdTokenAsync();

            // Endpoint do Firebase Storage (API v0)
            var uploadUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o?name={Uri.EscapeDataString(fileName)}";

            if (!string.IsNullOrEmpty(token))
            {
                uploadUrl += $"&uploadType=media&auth={token}";
            }
            else
            {
                // se você deixou as regras públicas para teste, isso ainda funciona
                uploadUrl += "&uploadType=media";
            }

            try
            {
                using var content = new StreamContent(fileStream);
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                var response = await _http.PostAsync(uploadUrl, content);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        $"Erro ao enviar para Firebase Storage. Status: {(int)response.StatusCode} - {response.ReasonPhrase}\nResposta: {json}");
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Estrutura típica:
                // {
                //   "name": "users/uid/profile_xxx.jpg",
                //   "bucket": "...",
                //   "downloadTokens": "abc-123-xyz"
                // }
                if (!root.TryGetProperty("name", out var nameProp))
                    throw new Exception("Resposta do Firebase Storage não contém 'name'.");

                var storedName = nameProp.GetString() ?? fileName;
                string? downloadToken = null;

                if (root.TryGetProperty("downloadTokens", out var tokenProp))
                {
                    downloadToken = tokenProp.GetString();
                }

                // Monta URL pública
                var baseUrl =
                    $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{Uri.EscapeDataString(storedName)}?alt=media";

                if (!string.IsNullOrEmpty(downloadToken))
                    baseUrl += $"&token={downloadToken}";

                return baseUrl;
            }
            catch (Exception ex)
            {
                throw new Exception("Falha ao fazer upload no Firebase Storage: " + ex.Message, ex);
            }
        }

        /// <summary>
        /// Mantido para compatibilidade: upload de imagem (usa image/jpeg).
        /// </summary>
        /// <param name="fileStream">Stream da imagem</param>
        /// <param name="fileName">Caminho/nome do arquivo dentro do bucket</param>
        public Task<string?> UploadImageAsync(Stream fileStream, string fileName)
        {
            // Reaproveita o método genérico, só fixando o contentType de imagem
            return UploadFileAsync(fileStream, fileName, "image/jpeg");
        }
    }
}
