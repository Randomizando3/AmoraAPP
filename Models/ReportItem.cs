using System;

namespace AmoraApp.Models
{
    public class ReportItem
    {
        public string Id { get; set; } = string.Empty;

        // "post" | "profile"
        public string Type { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string ReporterUserId { get; set; } = string.Empty;

        // alvo
        public string TargetUserId { get; set; } = string.Empty;
        public string TargetUserName { get; set; } = string.Empty;

        // se for denúncia de post
        public string PostId { get; set; } = string.Empty;
        public string PostTextPreview { get; set; } = string.Empty;
        public string PostImageUrl { get; set; } = string.Empty;

        // auditoria
        public long CreatedAtUtcMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public string AppArea { get; set; } = "feed";
        public string ExtraDetails { get; set; } = string.Empty;
    }
}
