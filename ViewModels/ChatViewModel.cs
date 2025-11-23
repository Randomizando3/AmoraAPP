using AmoraApp.Models;
using AmoraApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AmoraApp.ViewModels
{
    public partial class ChatViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<ChatMessage> messages = new();

        [ObservableProperty]
        private string newMessageText = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        public string ChatId { get; private set; } = string.Empty;
        public string OtherUserId { get; }
        public string OtherUserName { get; }

        // Pega o id do usuário logado de algum lugar (ajusta se você usar outro esquema)
        private string CurrentUserId => Preferences.Get("CurrentUserId", string.Empty);

        public IAsyncRelayCommand LoadMessagesCommand { get; }
        public IAsyncRelayCommand SendMessageCommand { get; }

        public ChatViewModel(string chatId, string otherUserId, string otherUserName)
        {
            ChatId = chatId;
            OtherUserId = otherUserId;
            OtherUserName = otherUserName;

            LoadMessagesCommand = new AsyncRelayCommand(LoadMessagesAsync);
            SendMessageCommand = new AsyncRelayCommand(SendMessageAsync);
        }

        public async Task InitializeAsync()
        {
            await LoadMessagesAsync();
        }

        private async Task LoadMessagesAsync()
        {
            if (IsBusy) return;
            if (string.IsNullOrWhiteSpace(ChatId))
                return;

            try
            {
                IsBusy = true;

                var list = await ChatService.Instance.GetMessagesAsync(ChatId, 80);

                Messages.Clear();

                foreach (var msg in list.OrderBy(m => m.CreatedAt))
                {
                    Messages.Add(msg);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SendMessageAsync()
        {
            var text = NewMessageText?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (string.IsNullOrWhiteSpace(CurrentUserId) || string.IsNullOrWhiteSpace(ChatId))
                return;

            var msg = new ChatMessage
            {
                ChatId = ChatId,
                SenderId = CurrentUserId,
                Text = text,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            await ChatService.Instance.SendMessageAsync(ChatId, msg);

            Messages.Add(msg);
            NewMessageText = string.Empty;
        }

        /// <summary>
        /// Helper para converter CreatedAt (unix ms) em DateTime local, para UI.
        /// </summary>
        public static DateTime ToLocalTime(ChatMessage msg)
        {
            if (msg?.CreatedAt > 0)
                return DateTimeOffset.FromUnixTimeMilliseconds(msg.CreatedAt).LocalDateTime;

            return DateTime.MinValue;
        }

        public bool IsMine(ChatMessage msg)
        {
            return msg?.SenderId == CurrentUserId;
        }
    }
}
