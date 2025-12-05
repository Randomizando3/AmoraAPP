using AmoraApp.Models;
using AmoraApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AmoraApp.ViewModels
{
    public partial class LikesViewModel : ObservableObject
    {
        private readonly MatchService _matchService;
        private readonly FirebaseAuthService _authService;
        private readonly FriendService _friendService;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool hasItems;

        [ObservableProperty]
        private bool hasNoItems;

        [ObservableProperty]
        private string title = "Quem me curtiu";

        public ObservableCollection<UserProfile> LikedMeUsers { get; } = new();

        public LikesViewModel()
            : this(MatchService.Instance, FirebaseAuthService.Instance, FriendService.Instance)
        {
        }

        public LikesViewModel(
            MatchService matchService,
            FirebaseAuthService authService,
            FriendService friendService)
        {
            _matchService = matchService;
            _authService = authService;
            _friendService = friendService;
        }

        public async Task InitializeAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                await LoadAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadAsync()
        {
            LikedMeUsers.Clear();

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me))
            {
                HasItems = false;
                HasNoItems = true;
                return;
            }

            var list = await _matchService.GetUsersWhoLikedMeAsync(me);

            foreach (var user in list)
                LikedMeUsers.Add(user);

            HasItems = LikedMeUsers.Count > 0;
            HasNoItems = !HasItems;
        }

        // ====== AÇÕES ======

        /// <summary>
        /// Curtir de volta (gera MATCH se for recíproco).
        /// </summary>
        [RelayCommand]
        private async Task LikeBackAsync(UserProfile? user)
        {
            if (user == null) return;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me)) return;

            var isMatch = await _matchService.LikeUserAsync(me, user.Id);

            if (isMatch)
            {
                await App.Current.MainPage.DisplayAlert(
                    "É um match! 💗",
                    $"Você e {user.DisplayName} combinaram!",
                    "OK");
            }

            // Tira da lista
            LikedMeUsers.Remove(user);
            HasItems = LikedMeUsers.Count > 0;
            HasNoItems = !HasItems;
        }

        /// <summary>
        /// Adicionar como amigo (mesma lógica do Discover).
        /// </summary>
        [RelayCommand]
        private async Task AddFriendAsync(UserProfile? user)
        {
            if (user == null) return;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me)) return;

            var other = user.Id;

            if (await _friendService.AreFriendsAsync(me, other))
            {
                await App.Current.MainPage.DisplayAlert("Já são amigos",
                    $"{user.DisplayName} já está na sua lista.", "OK");
                return;
            }

            if (await _friendService.HasIncomingRequestAsync(me, other))
            {
                await _friendService.AcceptFriendshipAsync(me, other);
                await App.Current.MainPage.DisplayAlert("Amizade aceita",
                    $"Agora você e {user.DisplayName} são amigos!", "OK");
                return;
            }

            if (await _friendService.HasOutgoingRequestAsync(me, other))
            {
                await App.Current.MainPage.DisplayAlert("Solicitação pendente",
                    $"Você já enviou uma solicitação.", "OK");
                return;
            }

            await _friendService.CreateFriendRequestAsync(me, other);

            await App.Current.MainPage.DisplayAlert("Solicitação enviada",
                $"Enviada para {user.DisplayName}.", "OK");
        }
    }
}
