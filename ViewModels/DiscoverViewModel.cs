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
using Microsoft.Maui.ApplicationModel;

namespace AmoraApp.ViewModels
{
    public partial class DiscoverViewModel : ObservableObject
    {
        private readonly MatchService _matchService;
        private readonly FirebaseAuthService _authService;
        private readonly FriendService _friendService;
        private readonly PresenceService _presenceService;

        // Lista completa de usuários recebida do Firebase
        private List<UserProfile> _allUsers = new();

        // Histórico p/ botão Rewind
        private readonly Stack<UserProfile> _history = new();

        // Minha localização
        private double? _myLat;
        private double? _myLon;

        // ====== Propriedades observáveis ======

        [ObservableProperty]
        private UserProfile? currentUser;

        [ObservableProperty]
        private ObservableCollection<UserProfile> users = new();

        [ObservableProperty]
        private bool hasUser;

        [ObservableProperty]
        private bool hasNoUser;

        [ObservableProperty]
        private string onlineText = string.Empty;

        [ObservableProperty]
        private string distanceText = string.Empty;

        // Gênero filtro (Girls / Boys / Both)
        [ObservableProperty]
        private string genderFilter = "Both";

        // Faixa etária
        [ObservableProperty]
        private int minAgeFilter = 18;

        [ObservableProperty]
        private int maxAgeFilter = 35;

        // Distância em km (200 = sem limite visualmente)
        [ObservableProperty]
        private int distanceFilterKm = 20;

        [ObservableProperty]
        private string professionFilter = string.Empty;

        [ObservableProperty]
        private string educationFilter = string.Empty;

        [ObservableProperty]
        private string religionFilter = string.Empty;

        [ObservableProperty]
        private string orientationFilter = string.Empty; // mantido só pra compat

        // Mantido só pra compatibilidade (não usado diretamente)
        [ObservableProperty]
        private string interestFilter = string.Empty;

        // ====== MULTI-SELEÇÃO DE INTERESSES ======

        public ObservableCollection<string> SelectedInterestFilters { get; } = new();

        public void ClearInterestFilters()
        {
            SelectedInterestFilters.Clear();
            InterestFilter = string.Empty;
        }

        public void AddInterestFilter(string interest)
        {
            if (!string.IsNullOrWhiteSpace(interest) &&
                !SelectedInterestFilters.Contains(interest))
            {
                SelectedInterestFilters.Add(interest);
            }
        }

        // ====== MULTI-SELEÇÃO DE ORIENTAÇÃO SEXUAL ======

        public ObservableCollection<string> SelectedOrientationFilters { get; } = new();

        public void ClearOrientationFilters()
        {
            SelectedOrientationFilters.Clear();
            OrientationFilter = string.Empty;
        }

        public void AddOrientationFilter(string orientation)
        {
            if (!string.IsNullOrWhiteSpace(orientation) &&
                !SelectedOrientationFilters.Contains(orientation))
            {
                SelectedOrientationFilters.Add(orientation);
            }
        }

        // ====== MULTI-SELEÇÃO DE "BUSCO POR" (amizade / namoro / etc.) ======

        // Itens selecionados no filtro
        public ObservableCollection<string> SelectedLookingForFilters { get; } = new();

        public void ClearLookingForFilters()
        {
            SelectedLookingForFilters.Clear();
        }

        public void AddLookingForFilter(string goal)
        {
            if (!string.IsNullOrWhiteSpace(goal) &&
                !SelectedLookingForFilters.Contains(goal))
            {
                SelectedLookingForFilters.Add(goal);
            }
        }

        // ====== Labels auxiliares p/ UI ======

        public string AgeRangeLabel => $"De {MinAgeFilter} até {MaxAgeFilter} anos";

        public string DistanceFilterLabel =>
            DistanceFilterKm >= 200 ? "Sem limite" : $"{DistanceFilterKm} km";

        // Atualiza rótulos quando valores mudam
        partial void OnMinAgeFilterChanged(int value)
        {
            if (MinAgeFilter < 18)
                MinAgeFilter = 18;

            if (MinAgeFilter > MaxAgeFilter)
                MaxAgeFilter = MinAgeFilter;

            OnPropertyChanged(nameof(AgeRangeLabel));
        }

        partial void OnMaxAgeFilterChanged(int value)
        {
            if (MaxAgeFilter > 120)
                MaxAgeFilter = 120;

            if (MaxAgeFilter < MinAgeFilter)
                MinAgeFilter = MaxAgeFilter;

            OnPropertyChanged(nameof(AgeRangeLabel));
        }

        partial void OnDistanceFilterKmChanged(int value)
        {
            if (DistanceFilterKm < 1)
                DistanceFilterKm = 1;
            if (DistanceFilterKm > 200)
                DistanceFilterKm = 200;

            OnPropertyChanged(nameof(DistanceFilterLabel));
        }

        // ====== Opções dos filtros ======

        public IList<string> EducationFilterOptions { get; } = new List<string>
        {
            "Ensino fundamental incompleto",
            "Ensino fundamental completo",
            "Ensino médio incompleto",
            "Ensino médio completo",
            "Técnico",
            "Superior incompleto",
            "Superior completo",
            "Pós-graduação",
            "Mestrado",
            "Doutorado",
            "Prefiro não dizer"
        };

        public IList<string> OrientationFilterOptions { get; } = new List<string>
        {
            "Heterossexual",
            "Homossexual",
            "Bissexual",
            "Pansexual",
            "Assexual",
            "Prefiro não dizer"
        };

        public IList<string> InterestFilterOptions { get; } = new List<string>
        {
            "Música","Filmes","Séries","Viagem","Games","Pets",
            "Gastronomia","Esportes","Livros","Tecnologia",
            "Arte","Natureza","Praia","Balada","Café"
        };

        // Opções de “Busco por”
        public IList<string> LookingForFilterOptions { get; } = new List<string>
        {
            "Amizade",
            "Namoro",
            "Casamento",
            "Casual"
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
        }

        // Quando CurrentUser muda → atualizar UI
        partial void OnCurrentUserChanged(UserProfile value)
        {
            HasUser = value != null;
            HasNoUser = !HasUser;

            _ = UpdateCurrentDistanceAndPresenceAsync();
        }

        // ================ CARREGAMENTO ================

        public async Task InitializeAsync()
        {
            if (CurrentUser != null || Users.Count > 0)
                return;

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var uid = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(uid))
                return;

            var myLoc = await LocationService.Instance.GetCurrentLocationAsync();
            if (myLoc != null)
            {
                _myLat = myLoc.Latitude;
                _myLon = myLoc.Longitude;
            }

            var list = await _matchService.GetUsersForDiscoverAsync(uid);

            _allUsers = list
                .Where(u => u != null)
                .Select(u =>
                {
                    u.PhotoUrl ??= string.Empty;
                    u.Interests ??= new List<string>();
                    // garante lista de "busco por" não nula
                    u.LookingFor ??= new List<string>();
                    return u;
                })
                .ToList();

            ApplyFiltersInternal();
        }

        // ================ FILTROS ================

        public void ApplyFilters() => ApplyFiltersInternal();

        private void ApplyFiltersInternal()
        {
            Users.Clear();
            _history.Clear();

            foreach (var u in _allUsers.Where(PassesFilters))
                Users.Add(u);

            CurrentUser = Users.FirstOrDefault();

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

            // Idade (entre min e max)
            if (u.Age < MinAgeFilter || u.Age > MaxAgeFilter)
                return false;

            // Distância (só filtra se slider < 200 = limite)
            if (DistanceFilterKm < 200 &&
                _myLat.HasValue && _myLon.HasValue &&
                u.Latitude != 0 && u.Longitude != 0)
            {
                var dist = HaversineKm(_myLat.Value, _myLon.Value, u.Latitude, u.Longitude);
                if (dist > DistanceFilterKm)
                    return false;
            }

            // Profissão
            if (!string.IsNullOrWhiteSpace(ProfessionFilter) &&
                (string.IsNullOrWhiteSpace(u.JobTitle) ||
                 !u.JobTitle.Contains(ProfessionFilter, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Escolaridade
            if (!string.IsNullOrWhiteSpace(EducationFilter) &&
                (string.IsNullOrWhiteSpace(u.EducationLevel) ||
                 !u.EducationLevel.Contains(EducationFilter, StringComparison.OrdinalIgnoreCase)))
                return false;

            // Religião
            if (!string.IsNullOrWhiteSpace(ReligionFilter) &&
                (string.IsNullOrWhiteSpace(u.Religion) ||
                 !u.Religion.Contains(ReligionFilter, StringComparison.OrdinalIgnoreCase)))
                return false;

            // ORIENTAÇÃO SEXUAL (multi)
            if (SelectedOrientationFilters.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(u.SexualOrientation))
                    return false;

                bool hasOrientationMatch = SelectedOrientationFilters.Any(o =>
                    u.SexualOrientation.Contains(o, StringComparison.OrdinalIgnoreCase));

                if (!hasOrientationMatch)
                    return false;
            }

            // INTERESSES (multi)
            if (SelectedInterestFilters.Count > 0)
            {
                var userInterests = u.Interests ?? new List<string>();

                bool hasMatch =
                    userInterests.Any(ui =>
                        SelectedInterestFilters.Any(fi =>
                            fi.Equals(ui, StringComparison.OrdinalIgnoreCase)));

                if (!hasMatch)
                    return false;
            }

            // "BUSCO POR" (multi) — usa a lista LookingFor do perfil
            if (SelectedLookingForFilters.Count > 0)
            {
                var goals = u.LookingFor ?? new List<string>();

                bool hasGoalMatch =
                    goals.Any(g =>
                        SelectedLookingForFilters.Any(f =>
                            f.Equals(g, StringComparison.OrdinalIgnoreCase)));

                if (!hasGoalMatch)
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

        // ================ LIKE / DISLIKE / FRIEND ================

        [RelayCommand]
        private async Task LikeAsync()
        {
            if (CurrentUser == null) return;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me)) return;

            var match = await _matchService.LikeUserAsync(me, CurrentUser.Id);

            if (match)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "É um match! 💗",
                        $"Você e {CurrentUser.DisplayName} combinaram!",
                        "OK");
                });
            }

            GoToNextUser();
        }

        [RelayCommand]
        private async Task DislikeAsync()
        {
            if (CurrentUser == null) return;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me)) return;

            await _matchService.DislikeUserAsync(me, CurrentUser.Id);

            GoToNextUser();
        }

        [RelayCommand]
        private async Task AddFriendAsync()
        {
            if (CurrentUser == null) return;

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me)) return;

            var other = CurrentUser.Id;

            if (await _friendService.AreFriendsAsync(me, other))
            {
                await App.Current.MainPage.DisplayAlert("Já são amigos",
                    $"{CurrentUser.DisplayName} já está na sua lista.", "OK");
                return;
            }

            if (await _friendService.HasIncomingRequestAsync(me, other))
            {
                await _friendService.AcceptFriendshipAsync(me, other);
                await App.Current.MainPage.DisplayAlert("Amizade aceita",
                    $"Agora você e {CurrentUser.DisplayName} são amigos!", "OK");
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
                $"Enviada para {CurrentUser.DisplayName}.", "OK");
        }

        [RelayCommand]
        private void Rewind()
        {
            if (_history.Count == 0) return;

            var previous = _history.Pop();
            if (CurrentUser != null)
                Users.Insert(0, CurrentUser);

            CurrentUser = previous;
        }

        private void GoToNextUser()
        {
            if (CurrentUser == null) return;

            var cur = CurrentUser;

            _history.Push(cur);

            if (Users.Contains(cur))
                Users.Remove(cur);

            CurrentUser = Users.FirstOrDefault();
        }

        // ================ DISTÂNCIA / PRESENÇA ================

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
                var d = HaversineKm(_myLat.Value, _myLon.Value,
                                    CurrentUser.Latitude, CurrentUser.Longitude);

                DistanceText = d < 1
                    ? "Menos de 1 km de você"
                    : $"{Math.Round(d)} km de você";
            }
            else
            {
                DistanceText = string.Empty;
            }

            // Presença
            try
            {
                var presence = await _presenceService.GetPresenceAsync(CurrentUser.Id);
                if (presence == null)
                {
                    OnlineText = string.Empty;
                    return;
                }

                if (presence.Value.IsOnline)
                    OnlineText = "Online";
                else
                {
                    var last = DateTimeOffset.FromUnixTimeSeconds(presence.Value.LastSeenUtc).ToLocalTime();
                    var diff = DateTimeOffset.Now - last;

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
                OnlineText = string.Empty;
            }
        }

        // ================ HAVERSINE ================

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;

            double dLat = ToRadians(lat2 - lat1);
            double dLon = ToRadians(lon2 - lon1);

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) *
                Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) *
                Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private static double ToRadians(double deg) => deg * Math.PI / 180.0;
    }
}
