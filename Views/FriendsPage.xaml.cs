using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class FriendsPage : ContentPage
    {
        private readonly FirebaseAuthService _authService = FirebaseAuthService.Instance;
        private readonly FirebaseDatabaseService _dbService = FirebaseDatabaseService.Instance;
        private readonly FriendService _friendService = FriendService.Instance;

        public ObservableCollection<FriendListItem> Friends { get; } = new();

        public FriendsPage()
        {
            InitializeComponent();
            BindingContext = this;
            _ = LoadFriendsAsync();
        }

        private async Task LoadFriendsAsync()
        {
            Friends.Clear();

            var meId = _authService.CurrentUserUid;
            if (string.IsNullOrEmpty(meId))
                return;

            var ids = await _friendService.GetFriendsAsync(meId);

            foreach (var id in ids)
            {
                var profile = await _dbService.GetUserProfileAsync(id);
                if (profile == null) continue;

                Friends.Add(new FriendListItem
                {
                    UserId = profile.Id,
                    DisplayName = profile.DisplayName,
                    Email = profile.Email,
                    PhotoUrl = profile.PhotoUrl
                });
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnFriendTapped(object sender, EventArgs e)
        {
            if ((e as TappedEventArgs)?.Parameter is not FriendListItem item)
                return;

            var meId = _authService.CurrentUserUid;
            if (string.IsNullOrEmpty(meId))
                return;

            // ActionSheet com "Cutucar" em cima
            var action = await DisplayActionSheet(
                item.DisplayName,
                "Cancelar",
                null,
                "Cutucar",
                "Excluir amigo",
                "Bloquear");

            if (action == "Cancelar" || string.IsNullOrEmpty(action))
                return;

            if (action == "Cutucar")
            {
                await PokeFriendAsync(meId, item);
                return;
            }

            if (action == "Excluir amigo")
            {
                await _friendService.RemoveFriendAsync(meId, item.UserId);
                Friends.Remove(item);
            }
            else if (action == "Bloquear")
            {
                await _friendService.RemoveFriendAsync(meId, item.UserId);
                await _friendService.CreateBlockAsync(meId, item.UserId);
                Friends.Remove(item);
            }
        }

        /// <summary>
        /// Envia uma mensagem de "cutucar" para o chat da pessoa.
        /// </summary>
        private async Task PokeFriendAsync(string meId, FriendListItem friend)
        {
            try
            {
                // Meu perfil para pegar o nome
                var myProfile = await _dbService.GetUserProfileAsync(meId);
                var myName = myProfile?.DisplayName ?? "Alguém";

                // Garante que existe chat entre nós dois
                var chatId = await ChatService.Instance.GetOrCreateChatAsync(meId, friend.UserId);

                // Monta mensagem de cutucada
                var msg = new ChatMessage
                {
                    ChatId = chatId,
                    SenderId = meId,
                    Text = $"{myName} cutucou você 👈",
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ReadBy = new System.Collections.Generic.Dictionary<string, bool>
                    {
                        [meId] = true
                    }
                };

                // Envia para o Firebase
                await ChatService.Instance.SendMessageAsync(chatId, msg);

                await DisplayAlert("Cutucada enviada",
                    $"Você cutucou {friend.DisplayName}.",
                    "OK");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FriendsPage] Erro ao cutucar: {ex}");
                await DisplayAlert("Erro",
                    "Não foi possível enviar a cutucada.",
                    "OK");
            }
        }
    }

    public class FriendListItem
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;
    }
}
