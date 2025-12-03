using System;

namespace AmoraApp.Models
{
    /// <summary>
    /// Item usado na lista de conversas (ChatListPage) e carrosséis.
    /// Funciona tanto para chats 1x1 quanto para grupos.
    /// </summary>
    public class ChatItem
    {
        public string ChatId { get; set; } = string.Empty;

        // Para chats 1x1: id do outro usuário
        // Em grupos pode ficar vazio (ou ser ignorado).
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;

        // Relacionamentos (apenas 1x1)
        public bool IsMatch { get; set; }
        public bool IsFriend { get; set; }

        // Favorito (1x1 + grupos)
        public bool IsFavorite { get; set; }

        // Se o chat está bloqueado para o usuário logado
        public bool IsBlocked { get; set; }

        // Preview e metadados da última mensagem
        public string LastMessagePreview { get; set; } = "Comece a conversar 👋";
        public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

        public int UnreadCount { get; set; } = 0;

        // ===== GRUPOS =====

        /// <summary>
        /// Indica se este ChatItem representa um grupo.
        /// </summary>
        public bool IsGroup { get; set; }

        /// <summary>
        /// Nome do grupo (sem o prefixo "Grupo ").
        /// </summary>
        public string GroupName { get; set; } = string.Empty;

        /// <summary>
        /// Quantidade de membros do grupo.
        /// </summary>
        public int MembersCount { get; set; }

        /// <summary>
        /// Se o usuário logado é o administrador do grupo.
        /// </summary>
        public bool IsGroupAdmin { get; set; }
    }
}
