using AmoraApp.Models;
using AmoraApp.Services;
using AmoraApp.ViewModels;
using AmoraApp.Views;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AmoraApp.Views
{
    public partial class LikesReceivedPage : ContentPage
    {
        public ObservableCollection<UserProfile> Likes { get; } = new();

        private readonly MatchService _matchService;
        private readonly FriendService _friendService;
        private readonly FirebaseAuthService _authService;

        public LikesReceivedPage()
        {
            InitializeComponent();

            _matchService = MatchService.Instance;
            _friendService = FriendService.Instance;
            _authService = FirebaseAuthService.Instance;

            BindingContext = this;

            // Commands usados no XAML
            MatchCommand = new Command<UserProfile>(async u => await OnMatchAsync(u));
            AddFriendCommand = new Command<UserProfile>(async u => await OnAddFriendAsync(u));
        }

        public Command<UserProfile> MatchCommand { get; }
        public Command<UserProfile> AddFriendCommand { get; }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadLikesAsync();
        }

        private async Task LoadLikesAsync()
        {
            Likes.Clear();

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me))
                return;

            try
            {
                // usuários que me deram like
                var raw = await _matchService.GetUsersWhoLikedMeAsync(me);
                foreach (var u in raw)
                {
                    if (u == null) continue;
                    u.PhotoUrl ??= string.Empty;
                    Likes.Add(u);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LikesReceivedPage] Erro ao carregar likes: " + ex);
            }
        }

        private async Task OnMatchAsync(UserProfile? user)
        {
            if (user == null) return;

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

                // Opcional: remove da lista depois
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

        private async void OnBackButtonClicked(object sender, EventArgs e)
        {
            try
            {
                await Navigation.PopAsync();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Tap no card inteiro → abre o Discover focado nesse usuário.
        /// </summary>
        private async void OnCardTapped(object sender, EventArgs e)
        {
            try
            {
                if (sender is not Frame frame)
                    return;

                if (frame.BindingContext is not UserProfile user || user == null)
                    return;

                // Cria um DiscoverViewModel novo
                var vm = new DiscoverViewModel();
                await vm.InitializeAsync();

                // tenta achar o user carregado pelo Discover
                var existing = vm.Users.FirstOrDefault(u => u.Id == user.Id);

                if (existing != null)
                {
                    // coloca esse usuário na frente da pilha
                    vm.Users.Remove(existing);
                    vm.Users.Insert(0, existing);
                    vm.CurrentUser = existing;
                }
                else
                {
                    // se não estiver na lista (algum filtro), injeta na frente
                    vm.Users.Insert(0, user);
                    vm.CurrentUser = user;
                }

                var page = new DiscoverPage(vm);
                await Navigation.PushAsync(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[LikesReceivedPage] Erro ao abrir Discover: " + ex);
                await DisplayAlert("Erro",
                    "Não foi possível abrir o perfil no Discover.",
                    "OK");
            }
        }
    }
}
