using System;

namespace AmoraApp.Models
{
    public class SupportTicket
    {
        public string Id { get; set; } = string.Empty;

        // Se logado, opcional
        public string UserId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        // "open" | "answered" | "closed"
        public string Status { get; set; } = "open";

        public long CreatedAtUtcMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // resposta admin
        public string Answer { get; set; } = string.Empty;
        public string AnsweredByAdminUid { get; set; } = string.Empty;
        public long AnsweredAtUtcMs { get; set; } = 0;
    }
}
