using System;
using System.Collections.Generic;

namespace AmoraApp.Models
{
    public class ChatMessage
    {
        public string Id { get; set; } = string.Empty;

        // ID do chat (ex: userA_userB)
        public string ChatId { get; set; } = string.Empty;

        // Quem enviou
        public string SenderId { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        // Unix timestamp em milissegundos
        public long CreatedAt { get; set; }

        // Se a mensagem já foi lida (para o usuário local)
        public bool IsRead { get; set; } = false;

        // Mapa de quem já leu a mensagem (para double-check)
        // key = userId, value = true (já leu)
        public Dictionary<string, bool>? ReadBy { get; set; }
    }
}
