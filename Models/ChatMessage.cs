using System;
using System.Collections.Generic;

namespace AmoraApp.Models
{
    public class ChatMessage
    {
        public string Id { get; set; } = string.Empty;

        // ID do chat (ex: userA_userB ou group_...)
        public string ChatId { get; set; } = string.Empty;

        // Quem enviou
        public string SenderId { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        // Imagem opcional (base64)
        public string? ImageBase64 { get; set; } = null;

        // Áudio opcional (URL no Firebase Storage)
        public string? AudioUrl { get; set; } = null;

        // Unix timestamp em milissegundos
        public long CreatedAt { get; set; }

        // Se a mensagem já foi lida (para o usuário local)
        public bool IsRead { get; set; } = false;

        // Mapa de quem já leu a mensagem (para double-check)
        public Dictionary<string, bool>? ReadBy { get; set; }
    }
}
