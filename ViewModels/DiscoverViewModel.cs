using System;
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
    // Chip de interesse para os filtros
    public partial class FilterInterestItem : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private bool isSelected;

        public FilterInterestItem() { }

        public FilterInterestItem(string name, bool isSelected = false)
        {
            this.name = name;
            this.isSelected = isSelected;
        }
    }

    public partial class DiscoverViewModel : ObservableObject
    {
        private readonly MatchService _matchService;
        private readonly FirebaseAuthService _authService;
        private readonly FriendService _friendService;
        private readonly PresenceService _presenceService;

        // Lista completa que veio do Firebase
        private List<UserProfile> _allUsers = new();

        // Histórico simples para o botão Rewind (voltar 1 card)
        private readonly Stack<UserProfile> _history = new();

        // Minha posição atual (pra cálculo de distância)
        private double? _myLat;
        private double? _myLon;

        // ====== propriedades observáveis ======

        [ObservableProperty]
        private UserProfile? currentUser;

        [ObservableProperty]
        private ObservableCollection<UserProfile> users = new();

        // Indica se há usuário atual
        [ObservableProperty]
        private bool hasUser;

        [ObservableProperty]
        private bool hasNoUser;

        // Texto “Online / Offline”
        [ObservableProperty]
        private string onlineText = string.Empty;

        // Texto “X km away”
        [ObservableProperty]
        private string distanceText = string.Empty;

        // Gênero filtro (Girls / Boys / Both)
        [ObservableProperty]
        private string genderFilter = "Both";

        // Idade mínima fixa 18
        public int MinAgeFilter => 18;

        [ObservableProperty]
        private int maxAgeFilter = 35;

        // Distância em km
        [ObservableProperty]
        private int distanceFilterKm = 20;

        // Filtros extras
        [ObservableProperty]
        private string professionFilter = string.Empty;

        [ObservableProperty]
        private string educationFilter = string.Empty;

        [ObservableProperty]
        private string religionFilter = string.Empty;

        [ObservableProperty]
        private string orientationFilter = string.Empty;

        // Lista de nomes de interesses selecionados
        public List<string> InterestFilter { get; set; } = new();

        // Chips de interesses disponíveis no filtro
        [ObservableProperty]
        private ObservableCollection<FilterInterestItem> availableFilterInterests = new();

        // Interesses padrão (mesmos do perfil)
        private static readonly string[] DefaultInterests =
        {
            "Música","Filmes","Séries","Viagem","Games","Pets",
            "Gastronomia","Esportes","Livros","Tecnologia",
            "Arte","Natureza","Praia","Balada","Café"
        };

        public DiscoverViewModel()
            : this(MatchService.Instance, FirebaseAuthService.Instance)
        {
        }

        public DiscoverViewModel(MatchService matchService, FirebaseAuthService authService)
        {
            _matchService = matchService;
            _authService = authService;
            _friendService = FriendService.Instance;
            _presenceService = PresenceService.Instance;

            InitFilterInterests();
        }

        // Quando CurrentUser muda, atualiza flags e presença/distância
        partial void OnCurrentUserChanged(UserProfile value)
        {
            HasUser = value != null;
            HasNoUser = !HasUser;

            _ = UpdateCurrentDistanceAndPresenceAsync();
        }

        // Inicializa chips de interesses do filtro
        private void InitFilterInterests()
        {
            AvailableFilterInterests.Clear();

            foreach (var name in DefaultInterests)
            {
                bool selected = InterestFilter.Any(i =>
                    string.Equals(i, name, StringComparison.OrdinalIgnoreCase));

                AvailableFilterInterests.Add(new FilterInterestItem(name, selected));
            }
        }

        // Re-sincroniza chips quando filtros mudarem
        public void SyncFilterInterestsFromFilterList()
        {
            foreach (var item in AvailableFilterInterests)
            {
                item.IsSelected = InterestFilter.Any(i =>
                    string.Equals(i, item.Name, StringComparison.OrdinalIgnoreCase));
            }
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

            // Tenta pegar minha localização atual (GPS/IP/debug)
            var myLoc = await LocationService.Instance.GetCurrentLocationAsync();
            if (myLoc != null)
            {
                _myLat = myLoc.Latitude;
                _myLon = myLoc.Longitude;
            }

            var list = await _matchService.GetUsersForDiscoverAsync(uid);

            // Garante que campos não venham nulos
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

            // Se não houver usuários, apaga textos de status
            if (CurrentUser == null)
            {
                OnlineText = string.Empty;
                DistanceText = string.Empty;
            }
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

            // Distância (se tivermos minha localização e a do outro)
            if (_myLat.HasValue && _myLon.HasValue &&
                u.Latitude != 0 && u.Longitude != 0)
            {
                var d = HaversineKm(
                    _myLat.Value, _myLon.Value,
                    u.Latitude, u.Longitude);

                if (d > DistanceFilterKm)
                    return false;
            }

            // Profissão
            if (!string.IsNullOrWhiteSpace(ProfessionFilter))
            {
                if (string.IsNullOrWhiteSpace(u.JobTitle) ||
                    !u.JobTitle.Contains(ProfessionFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Escolaridade
            if (!string.IsNullOrWhiteSpace(EducationFilter))
            {
                if (string.IsNullOrWhiteSpace(u.EducationLevel) ||
                    !u.EducationLevel.Contains(EducationFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Religião
            if (!string.IsNullOrWhiteSpace(ReligionFilter))
            {
                if (string.IsNullOrWhiteSpace(u.Religion) ||
                    !u.Religion.Contains(ReligionFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Orientação sexual
            if (!string.IsNullOrWhiteSpace(OrientationFilter))
            {
                if (string.IsNullOrWhiteSpace(u.SexualOrientation) ||
                    !u.SexualOrientation.Contains(OrientationFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Interesses (pelo menos 1 em comum)
            if (InterestFilter != null && InterestFilter.Count > 0)
            {
                var userInterests = u.Interests ?? new List<string>();

                bool hasMatch = userInterests.Any(ui =>
                    InterestFilter.Any(fi =>
                        string.Equals(fi, ui, StringComparison.OrdinalIgnoreCase)));

                if (!hasMatch)
                    return false;
            }

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

        // ================== DISTÂNCIA / PRESENÇA ==================

        private async Task UpdateCurrentDistanceAndPresenceAsync()
        {
            if (CurrentUser == null)
            {
                OnlineText = string.Empty;
                DistanceText = string.Empty;
                return;
            }

            // Distância
            if (_myLat.HasValue && _myLon.HasValue &&
                CurrentUser.Latitude != 0 && CurrentUser.Longitude != 0)
            {
                var d = HaversineKm(
                    _myLat.Value, _myLon.Value,
                    CurrentUser.Latitude, CurrentUser.Longitude);

                if (d < 1)
                    DistanceText = "Menos de 1 km de você";
                else
                    DistanceText = $"{Math.Round(d)} km de você";
            }
            else
            {
                DistanceText = string.Empty;
            }

            // Presença (online / último acesso)
            try
            {
                var presence = await _presenceService.GetPresenceAsync(CurrentUser.Id);
                if (presence == null)
                {
                    OnlineText = string.Empty;
                    return;
                }

                if (presence.Value.IsOnline)
                {
                    OnlineText = "Online";
                }
                else
                {
                    var lastSeen = DateTimeOffset.FromUnixTimeSeconds(presence.Value.LastSeenUtc).ToLocalTime();
                    var diff = DateTimeOffset.Now - lastSeen;

                    if (diff.TotalMinutes < 1)
                        OnlineText = "Visto agora";
                    else if (diff.TotalHours < 1)
                        OnlineText = $"Visto há {Math.Round(diff.TotalMinutes)} min";
                    else if (diff.TotalDays < 1)
                        OnlineText = $"Visto há {Math.Round(diff.TotalHours)} h";
                    else
                        OnlineText = $"Visto há {Math.Round(diff.TotalDays)} d";
                }
            }
            catch
            {
                // se der erro de rede, só deixa vazio
                OnlineText = string.Empty;
            }
        }

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0; // raio médio da Terra em km

            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private static double ToRadians(double deg) => deg * Math.PI / 180.0;
    }
}
