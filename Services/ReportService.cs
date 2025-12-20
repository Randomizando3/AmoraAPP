using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Reflection;
using AmoraApp.Config;
using AmoraApp.Models;

namespace AmoraApp.Services
{
    public class ReportService
    {
        public static ReportService Instance { get; } = new ReportService();

        private readonly HttpClient _http = new HttpClient();

        private ReportService() { }

        public async Task SubmitReportAsync(ReportItem report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));

            var baseUrl = (FirebaseSettings.DatabaseUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("FirebaseSettings.DatabaseUrl não configurado.");

            baseUrl = baseUrl.TrimEnd('/');

            // garante Id (pra ficar previsível no admin)
            if (string.IsNullOrWhiteSpace(report.Id))
                report.Id = $"r_{Guid.NewGuid():N}";

            // tenta token (se regras exigirem)
            var token = await TryGetIdTokenAsync();

            // PUT em /reports/{id}.json
            var url = $"{baseUrl}/reports/{Uri.EscapeDataString(report.Id)}.json";
            if (!string.IsNullOrWhiteSpace(token))
                url += $"?auth={Uri.EscapeDataString(token)}";

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PutAsync(url, content);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Falha ao enviar denúncia. HTTP {(int)resp.StatusCode}. {body}");
            }
        }


        /// <summary>
        /// Tenta obter o IdToken via reflexão para evitar dependência rígida de assinatura.
        /// Retorna null se não conseguir.
        /// </summary>
        private static async Task<string?> TryGetIdTokenAsync()
        {
            try
            {
                var user = FirebaseAuthService.Instance.GetCurrentUser();
                if (user == null) return null;

                // procura método: Task<string> GetIdTokenAsync(bool forceRefresh)
                var method = user.GetType().GetMethod("GetIdTokenAsync", BindingFlags.Instance | BindingFlags.Public);
                if (method == null) return null;

                var pars = method.GetParameters();
                if (pars.Length == 1 && pars[0].ParameterType == typeof(bool))
                {
                    var taskObj = method.Invoke(user, new object[] { false });
                    if (taskObj is Task<string> t)
                        return await t;
                }
            }
            catch
            {
                // ignora (regras podem permitir sem token)
            }

            return null;
        }
    }
}
