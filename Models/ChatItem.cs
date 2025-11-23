// Models/ChatItem.cs
using System;

namespace AmoraApp.Models
{
    /// <summary>
    /// Item usado na lista de conversas (ChatListPage).
    /// </summary>
    public class ChatItem
    {
        public string ChatId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;

        public bool IsMatch { get; set; }
        public bool IsFriend { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsBlocked { get; set; }

        public string LastMessagePreview { get; set; } = "Comece a conversar 👋";
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public int UnreadCount { get; set; } = 0;
    }
}
