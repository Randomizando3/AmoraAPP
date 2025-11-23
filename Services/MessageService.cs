using AmoraApp.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmoraApp.Services
{
    /// <summary>
    /// Serviço fino em cima do ChatService, para manter compatibilidade
    /// com o código mais antigo, mas já usando o novo modelo de ChatMessage.
    /// </summary>
    public class MessageService
    {
        public static MessageService Instance { get; } = new MessageService();

        private MessageService()
        {
        }

        /// <summary>
        /// Busca mensagens de um chat específico.
        /// </summary>
        public Task<IList<ChatMessage>> GetMessagesAsync(string chatId, int limit = 80)
        {
            return ChatService.Instance.GetMessagesAsync(chatId, limit);
        }

        /// <summary>
        /// Envia uma nova mensagem neste chat.
        /// </summary>
        public async Task SendMessageAsync(string chatId, string senderId, string text)
        {
            if (string.IsNullOrWhiteSpace(chatId))
                throw new ArgumentException("chatId é obrigatório.", nameof(chatId));

            if (string.IsNullOrWhiteSpace(senderId))
                throw new ArgumentException("senderId é obrigatório.", nameof(senderId));

            if (string.IsNullOrWhiteSpace(text))
                return;

            var msg = new ChatMessage
            {
                ChatId = chatId,
                SenderId = senderId,
                Text = text.Trim(),
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await ChatService.Instance.SendMessageAsync(chatId, msg);
        }
    }
}
