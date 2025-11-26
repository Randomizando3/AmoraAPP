using System;
using System.Text.Json.Serialization;

namespace AmoraApp.Models
{
    public class Comment
    {
        public string Id { get; set; } = string.Empty;
        public string PostId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Texto resumido para aparecer embaixo do post
        [JsonIgnore]
        public string ShortText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Text))
                    return string.Empty;

                const int max = 80;
                return Text.Length <= max
                    ? Text
                    : Text.Substring(0, max) + "...";
            }
        }
    }
}
