using AmoraApp.Models;
using AmoraApp.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace AmoraApp.Views
{
    public partial class LikesReceivedPage : ContentPage
    {
        public ObservableCollection<UserProfile> Likes { get; } = new();

        private readonly MatchService _matchService;
        private readonly FriendService _friendService;
        private readonly FirebaseAuthService _authService;
        private readonly PlanService _planService;

        // usado apenas para o overlay (grátis x plus/premium)
        public static readonly BindableProperty IsLockedProperty =
            BindableProperty.Create(
                nameof(IsLocked),
                typeof(bool),
                typeof(LikesReceivedPage),
                true);

        public bool IsLocked
        {
            get => (bool)GetValue(IsLockedProperty);
            set => SetValue(IsLockedProperty, value);
        }

        public LikesReceivedPage()
        {
            InitializeComponent();

            _matchService = MatchService.Instance;
            _friendService = FriendService.Instance;
            _authService = FirebaseAuthService.Instance;
            _planService = PlanService.Instance;

            BindingContext = this;

            MatchCommand = new Command<UserProfile>(async u => await OnMatchAsync(u));
            AddFriendCommand = new Command<UserProfile>(async u => await OnAddFriendAsync(u));
        }

        public Command<UserProfile> MatchCommand { get; }
        public Command<UserProfile> AddFriendCommand { get; }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me))
            {
                EmptyLabel.Text = "Faça login para ver quem curtiu você.";
                EmptyLabel.IsVisible = true;
                return;
            }

            // Plano atual (visual + bloqueio)
            var plan = await _planService.GetUserPlanAsync(me);
            var planName = _planService.GetPlanDisplayName(plan);
            PlanInfoLabel.Text = $"Plano atual: {planName}";

            IsLocked = !_planService.CanSeeLikesReceived(plan);
            LockOverlay.IsVisible = IsLocked;

            await LoadLikesAsync();
        }

        private async Task LoadLikesAsync()
        {
            Likes.Clear();
            EmptyLabel.IsVisible = false;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me))
                return;

            try
            {
                var raw = await _matchService.GetUsersWhoLikedMeAsync(me);
                bool hasAny = false;

                foreach (var u in raw)
                {
                    if (u == null) continue;
                    u.PhotoUrl ??= string.Empty;
                    Likes.Add(u);
                    hasAny = true;
                }

                if (!hasAny)
                {
                    EmptyLabel.Text = "Ainda ninguém curtiu você... continue dando likes! ❤️";
                    EmptyLabel.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LikesReceivedPage] Erro ao carregar likes: " + ex);
                EmptyLabel.Text = "Não foi possível carregar as curtidas.";
                EmptyLabel.IsVisible = true;
            }
        }

        // ========= AÇÕES CARD =========

        private async Task OnMatchAsync(UserProfile? user)
        {
            if (user == null) return;

            if (IsLocked)
            {
                await Shell.Current.GoToAsync("//upgrade");
                return;
            }

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me)) return;

            try
            {
                var isMatch = await _matchService.LikeUserAsync(me, user.Id);
                if (isMatch)
                {
                    await DisplayAlert("É um match! 💗",
                        $"Você e {user.DisplayName} combinaram!",
                        "OK");
                }

                Likes.Remove(user);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível registrar o like.\n" + ex.Message,
                    "OK");
            }
        }

        private async Task OnAddFriendAsync(UserProfile? user)
        {
            if (user == null) return;

            if (IsLocked)
            {
                await Shell.Current.GoToAsync("//upgrade");
                return;
            }

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me)) return;

            try
            {
                var other = user.Id;

                if (await _friendService.AreFriendsAsync(me, other))
                {
                    await DisplayAlert("Já são amigos",
                        $"{user.DisplayName} já está na sua lista.",
                        "OK");
                    return;
                }

                if (await _friendService.HasIncomingRequestAsync(me, other))
                {
                    await _friendService.AcceptFriendshipAsync(me, other);
                    await DisplayAlert("Amizade aceita",
                        $"Agora você e {user.DisplayName} são amigos!",
                        "OK");
                    Likes.Remove(user);
                    return;
                }

                if (await _friendService.HasOutgoingRequestAsync(me, other))
                {
                    await DisplayAlert("Solicitação pendente",
                        "Você já enviou uma solicitação para esta pessoa.",
                        "OK");
                    return;
                }

                await _friendService.CreateFriendRequestAsync(me, other);

                await DisplayAlert("Solicitação enviada",
                    $"Enviada para {user.DisplayName}.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Erro",
                    "Não foi possível enviar a solicitação de amizade.\n" + ex.Message,
                    "OK");
            }
        }

        private async void OnCardTapped(object sender, EventArgs e)
        {
            if (IsLocked)
            {
                await Shell.Current.GoToAsync("//upgrade");
                return;
            }

            await Shell.Current.GoToAsync("//discover");
        }

        // ========= NAVEGAÇÃO / UPGRADE =========

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PopAsync();
            }
            catch { }
        }

        private async void OnUpgradeClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//upgrade");
        }

        private async void OnUpgradeOverlayTapped(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("//upgrade");
        }
    }
}
