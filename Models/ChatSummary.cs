using System;

namespace AmoraApp.Models
{
    /// <summary>
    /// Resumo de conversa salvo em /conversations/{userId}/{otherUserId}
    /// </summary>
    public class ChatSummary
    {
        public string OtherUserId { get; set; } = string.Empty;

        public string LastMessageText { get; set; } = string.Empty;
        public string LastMessageFromId { get; set; } = string.Empty;
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public int UnreadCount { get; set; } = 0;
    }
}
