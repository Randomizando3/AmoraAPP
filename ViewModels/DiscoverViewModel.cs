using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui;

namespace AmoraApp.ViewModels
{
    public partial class DiscoverViewModel : ObservableObject
    {
        private readonly MatchService _matchService;
        private readonly FirebaseAuthService _authService;
        private readonly FriendService _friendService;

        // Lista completa que veio do Firebase
        private List<UserProfile> _allUsers = new();

        // Histórico simples para o botão Rewind (voltar 1 card)
        private readonly Stack<UserProfile> _history = new();

        [ObservableProperty]
        private UserProfile? currentUser;

        [ObservableProperty]
        private ObservableCollection<UserProfile> users = new();

        // ========= FILTROS BÁSICOS =========

        // Girls / Boys / Both
        [ObservableProperty]
        private string genderFilter = "Both";

        // Idade mínima fixa 18
        public int MinAgeFilter => 18;

        [ObservableProperty]
        private int maxAgeFilter = 35;

        // Por enquanto só visual
        [ObservableProperty]
        private int distanceFilterKm = 20;

        public DiscoverViewModel()
            : this(MatchService.Instance, FirebaseAuthService.Instance)
        {
        }

        public DiscoverViewModel(MatchService matchService, FirebaseAuthService authService)
        {
            _matchService = matchService;
            _authService = authService;
            _friendService = FriendService.Instance;
        }

        public async Task InitializeAsync()
        {
            if (CurrentUser != null || Users.Count > 0)
                return; // já carregou

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var uid = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var list = await _matchService.GetUsersForDiscoverAsync(uid);

            // Garante que Id está setado e não vem nulo
            _allUsers = list
                .Where(u => u != null)
                .Select(u =>
                {
                    u.PhotoUrl ??= string.Empty;
                    u.Gallery ??= new List<string>();
                    u.Interests ??= new List<string>();
                    return u;
                })
                .ToList();

            ApplyFiltersInternal();
        }

        // Chama quando mudar filtros
        public void ApplyFilters()
        {
            ApplyFiltersInternal();
        }

        private void ApplyFiltersInternal()
        {
            Users.Clear();
            _history.Clear();

            var filtered = _allUsers
                .Where(PassesFilters)
                .ToList();

            foreach (var u in filtered)
                Users.Add(u);

            CurrentUser = Users.FirstOrDefault();
        }

        private bool PassesFilters(UserProfile u)
        {
            // Gênero
            if (GenderFilter == "Girls" && !IsFemale(u.Gender))
                return false;

            if (GenderFilter == "Boys" && !IsMale(u.Gender))
                return false;

            // Idade
            if (u.Age < MinAgeFilter || u.Age > MaxAgeFilter)
                return false;

            // Distância ainda não filtra (sem localização real)
            return true;
        }

        private static bool IsFemale(string gender)
        {
            if (string.IsNullOrWhiteSpace(gender)) return false;
            gender = gender.ToLowerInvariant();
            return gender.Contains("fem") || gender.Contains("mulher");
        }

        private static bool IsMale(string gender)
        {
            if (string.IsNullOrWhiteSpace(gender)) return false;
            gender = gender.ToLowerInvariant();
            return gender.Contains("masc") || gender.Contains("homem");
        }

        // ============= AÇÕES DE LIKE / DISLIKE / ADD =============

        [RelayCommand]
        private async Task LikeAsync()
        {
            if (CurrentUser == null)
                return;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrEmpty(me))
                return;

            var target = CurrentUser;

            var isMatch = await _matchService.LikeUserAsync(me, target.Id);

            if (isMatch)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "É um match! 💗",
                        $"Você e {target.DisplayName} combinaram!",
                        "OK");
                });
            }

            GoToNextUser();
        }

        [RelayCommand]
        private async Task DislikeAsync()
        {
            if (CurrentUser == null)
                return;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrEmpty(me))
                return;

            var target = CurrentUser;

            await _matchService.DislikeUserAsync(me, target.Id);

            GoToNextUser();
        }

        /// <summary>
        /// Botão "+" no Discover → amizade
        /// </summary>
        [RelayCommand]
        private async Task AddFriendAsync()
        {
            if (CurrentUser == null)
                return;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrEmpty(me))
                return;

            var other = CurrentUser.Id;

            // 1) Já são amigos?
            if (await _friendService.AreFriendsAsync(me, other))
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Já são amigos",
                        $"{CurrentUser.DisplayName} já está na sua lista de amigos.",
                        "OK");
                });
                return;
            }

            // 2) O outro já enviou solicitação pra mim? (other -> me)
            if (await _friendService.HasIncomingRequestAsync(me, other))
            {
                await _friendService.AcceptFriendshipAsync(me, other);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Amizade aceita",
                        $"Você e {CurrentUser.DisplayName} agora são amigos.",
                        "OK");
                });
                return;
            }

            // 3) Eu já enviei solicitação pra ele(a)? (me -> other)
            if (await _friendService.HasOutgoingRequestAsync(me, other))
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Solicitação já enviada",
                        $"Você já enviou uma solicitação para {CurrentUser.DisplayName}.",
                        "OK");
                });
                return;
            }

            // 4) Nova solicitação
            await _friendService.CreateFriendRequestAsync(me, other);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await App.Current.MainPage.DisplayAlert(
                    "Solicitação enviada",
                    $"Sua solicitação de amizade foi enviada para {CurrentUser.DisplayName}.",
                    "OK");
            });
        }

        // Botão REWIND: volta para o último user que saiu da fila
        [RelayCommand]
        private void Rewind()
        {
            if (_history.Count == 0)
                return;

            var previous = _history.Pop();

            if (CurrentUser != null)
            {
                // devolve o atual para a fila (no começo)
                Users.Insert(0, CurrentUser);
            }

            CurrentUser = previous;
        }

        // Chamado internamente após like/dislike/swipe
        private void GoToNextUser()
        {
            if (CurrentUser == null)
                return;

            var current = CurrentUser;

            // Guarda no histórico
            _history.Push(current);

            // Remove do começo da fila, se ainda estiver lá
            if (Users.Contains(current))
                Users.Remove(current);

            // Próximo da fila vira CurrentUser
            CurrentUser = Users.FirstOrDefault();
        }
    }
}
