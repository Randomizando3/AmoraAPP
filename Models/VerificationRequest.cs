using System;

namespace AmoraApp.Models
{
    public class VerificationRequest
    {
        // id espelhado (para facilitar)
        public string Id { get; set; } = string.Empty;

        // uid do user
        public string UserId { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserPhotoUrl { get; set; } = string.Empty;

        // uploads
        public string SelfieUrl { get; set; } = string.Empty;
        public string DocumentUrl { get; set; } = string.Empty;

        // "pending" | "approved" | "rejected"
        public string Status { get; set; } = "pending";

        public long CreatedAtUtcMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // admin
        public string ReviewedByAdminUid { get; set; } = string.Empty;
        public long ReviewedAtUtcMs { get; set; } = 0;
        public string AdminNote { get; set; } = string.Empty;
    }
}
