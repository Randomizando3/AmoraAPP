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

        // ===== CONFIG BOOST (ajuste aqui o comportamento geral) =====

        // Durações por plano
        private static readonly TimeSpan FreeBoostDuration = TimeSpan.FromMinutes(15);   // Free com token
        private static readonly TimeSpan PlusBoostDuration = TimeSpan.FromHours(3);      // Plus: 3h
        private static readonly TimeSpan PremiumBoostDuration = TimeSpan.FromHours(3);   // Premium: 3h

        // Limites diários por plano
        private const int FreeBoostDailyLimit = 1;      // Free: 1 boost/dia
        private const int PlusBoostDailyLimit = 3;      // Plus: 3 boosts/dia
        private const int PremiumBoostDailyLimit = 5;   // Premium: 5 boosts/dia


        // Lista completa de usuários recebida do Firebase
        private List<UserProfile> _allUsers = new();

        // Histórico p/ botão Rewind
        private readonly Stack<UserProfile> _history = new();

        // Planos
        private readonly PlanService _planService = PlanService.Instance;

        // Banco (pra carregar meu perfil e salvar boost)
        private readonly FirebaseDatabaseService _dbService = FirebaseDatabaseService.Instance;

        // Minha localização
        private double? _myLat;
        private double? _myLon;

        // Meu perfil (para plano, boosts, tokens, etc.)
        private UserProfile? _myProfile;

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
            //_planService = PlanService.Instance;
            //_dbService = FirebaseDatabaseService.Instance;
        }

        /// <summary>
        /// Construtor para abrir Discover focado em um único usuário (ex: vindo do grupo).
        /// </summary>
        public DiscoverViewModel(UserProfile singleUser)
            : this(MatchService.Instance, FirebaseAuthService.Instance)
        {
            if (singleUser != null)
            {
                singleUser.PhotoUrl ??= string.Empty;
                singleUser.Interests ??= new List<string>();
                singleUser.LookingFor ??= new List<string>();

                _allUsers = new List<UserProfile> { singleUser };

                Users.Clear();
                Users.Add(singleUser);

                CurrentUser = singleUser;

                HasUser = true;
                HasNoUser = false;

                _ = UpdateCurrentDistanceAndPresenceAsync();
            }
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
            // Se já foi inicializado (ou veio de construtor singleUser), não recarrega
            if (CurrentUser != null || Users.Count > 0)
                return;

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            var uid = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(uid))
                return;

            // Minha localização
            var myLoc = await LocationService.Instance.GetCurrentLocationAsync();
            if (myLoc != null)
            {
                _myLat = myLoc.Latitude;
                _myLon = myLoc.Longitude;
            }

            // Meu perfil (para plano, tokens e boost)
            _myProfile = await _dbService.GetUserProfileAsync(uid);

            var list = await _matchService.GetUsersForDiscoverAsync(uid);

            _allUsers = list
                .Where(u => u != null)
                .Select(u =>
                {
                    u.PhotoUrl ??= string.Empty;
                    u.Interests ??= new List<string>();
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

            var filtered = _allUsers
                .Where(PassesFilters)
                .OrderByDescending(GetPlanPriority);  // PREMIUM, depois PLUS, depois FREE, com boost aplicado

            foreach (var u in filtered)
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

            // 1) verifica se o plano permite mais likes (free tem limite por dia)
            var canLike = await _planService.CanUseLikeAsync(me);
            if (!canLike)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Limite de likes atingido",
                        "No plano gratuito você tem um número limitado de curtidas por dia. " +
                        "Assine o Plus ou Premium para ter likes ilimitados.",
                        "OK");
                });
                return;
            }

            // 2) registra o like e verifica match
            var match = await _matchService.LikeUserAsync(me, CurrentUser.Id);

            // contabiliza o uso para plano grátis
            await _planService.RegisterLikeAsync(me);

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
        private async Task RewindAsync()
        {
            // Se não há histórico, não tem pra onde voltar
            if (_history.Count == 0)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Nada para voltar",
                        "Você ainda não passou nenhum perfil para trás.",
                        "OK");
                });
                return;
            }

            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me))
                return;

            // garante que meu perfil está carregado (pra ler o plano)
            if (_myProfile == null || _myProfile.Id != me)
            {
                _myProfile = await _dbService.GetUserProfileAsync(me);
            }

            var plan = _planService.ParsePlanFromString(_myProfile?.Plan ?? "Free");

            // Rewind é exclusivo para Plus e Premium
            if (plan == PlanType.Free)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Funcionalidade exclusiva",
                        "O botão de voltar é exclusivo para os planos Plus e Premium.\n" +
                        "Assine um plano para poder desfazer o último swipe.",
                        "OK");
                });
                return;
            }

            // Agora sim: volta o último perfil
            var previous = _history.Pop();

            // O CurrentUser atual volta pro topo da fila
            if (CurrentUser != null)
                Users.Insert(0, CurrentUser);

            CurrentUser = previous;
        }


        [RelayCommand]
        private async Task BoostAsync()
        {
            var me = _authService.CurrentUserUid;
            if (string.IsNullOrWhiteSpace(me))
                return;

            // garante que meu perfil está carregado
            if (_myProfile == null || _myProfile.Id != me)
            {
                _myProfile = await _dbService.GetUserProfileAsync(me);
                if (_myProfile == null)
                {
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await App.Current.MainPage.DisplayAlert(
                            "Erro",
                            "Não foi possível carregar seu perfil para aplicar o boost.",
                            "OK");
                    });
                    return;
                }
            }

            var plan = _planService.ParsePlanFromString(_myProfile.Plan);
            var now = DateTimeOffset.UtcNow;

            // Normaliza o dia (UTC) para meia-noite
            var todayUtcDate = now.Date; // DateTime UTC, sem hora
            var todayStart = new DateTimeOffset(todayUtcDate, TimeSpan.Zero).ToUnixTimeSeconds();

            // Se o dia salvo for diferente do dia de hoje -> zera contador
            if (_myProfile.BoostUsesDayUtc != todayStart)
            {
                _myProfile.BoostUsesDayUtc = todayStart;
                _myProfile.BoostUsesToday = 0;
            }

            // Define limite diário conforme plano
            int dailyLimit = plan switch
            {
                PlanType.Premium => PremiumBoostDailyLimit,
                PlanType.Plus => PlusBoostDailyLimit,
                _ => FreeBoostDailyLimit
            };

            // Verifica limite diário
            if (_myProfile.BoostUsesToday >= dailyLimit)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Limite diário atingido",
                        plan switch
                        {
                            PlanType.Premium => "Você já usou todos os boosts Premium disponíveis hoje. Volte amanhã para usar mais.",
                            PlanType.Plus => "Você já usou todos os boosts Plus disponíveis hoje. Volte amanhã para usar mais.",
                            _ => "Você já usou todos os boosts disponíveis hoje. Considere assinar o Plus ou Premium para ter mais impulsos diários.",
                        },
                        "OK");
                });
                return;
            }

            // Já tem boost ativo?
            if (_myProfile.IsBoostActive && _myProfile.BoostExpiresUtc > now.ToUnixTimeSeconds())
            {
                var remaining = DateTimeOffset.FromUnixTimeSeconds(_myProfile.BoostExpiresUtc) - now;
                var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Boost já ativo",
                        $"Seu perfil já está em destaque por mais {minutes} min.",
                        "OK");
                });
                return;
            }

            // Plano Free sem tokens não pode usar boost (além do limite diário)
            if (plan == PlanType.Free && _myProfile.ExtraBoostTokens <= 0)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await App.Current.MainPage.DisplayAlert(
                        "Boost indisponível",
                        "No plano gratuito o boost é liberado apenas com pacotes adicionais.\n" +
                        "Assine o Plus (5x) ou Premium (10x) para ter boosts incluídos e mais usos por dia.",
                        "OK");
                });
                return;
            }

            // Define multiplicador e duração conforme plano
            int multiplier;
            TimeSpan duration;

            switch (plan)
            {
                case PlanType.Premium:
                    multiplier = 10;                  // 10x para Premium
                    duration = PremiumBoostDuration;  // 3h (config acima)
                    break;

                case PlanType.Plus:
                    multiplier = 5;                  // 5x para Plus
                    duration = PlusBoostDuration;    // 3h (config acima)
                    break;

                default:
                    multiplier = 3;                  // Free com pacote avulso
                    duration = FreeBoostDuration;    // 15min (config acima)
                    _myProfile.ExtraBoostTokens = Math.Max(0, _myProfile.ExtraBoostTokens - 1);
                    break;
            }

            var expiresAt = now.Add(duration);

            _myProfile.IsBoostActive = true;
            _myProfile.BoostMultiplier = multiplier;
            _myProfile.BoostExpiresUtc = expiresAt.ToUnixTimeSeconds();
            _myProfile.BoostUsesToday += 1;

            await _dbService.SaveUserProfileAsync(_myProfile);

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var msg = plan switch
                {
                    PlanType.Premium => $"Seu perfil foi impulsionado em 10x por {duration.TotalMinutes} minutos. 💜",
                    PlanType.Plus => $"Seu perfil foi impulsionado em 5x por {duration.TotalMinutes} minutos. 💗",
                    _ => $"Seu perfil foi impulsionado por {duration.TotalMinutes} minutos. ✨"
                };

                await App.Current.MainPage.DisplayAlert("Boost ativado", msg, "OK");
            });

            // Reorganiza a lista considerando boosts
            ApplyFiltersInternal();
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

        // Prioridade da Fila de planos + boost
        private double GetPlanPriority(UserProfile u)
        {
            var plan = PlanService.Instance.ParsePlanFromString(u.Plan);

            var baseScore = plan switch
            {
                PlanType.Premium => 3,
                PlanType.Plus => 2,
                _ => 1
            };

            // BOOST: se tiver boost ativo e não expirado, multiplica a pontuação
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var boostMultiplier = 1.0;

            if (u.IsBoostActive && u.BoostExpiresUtc > now)
            {
                var mult = u.BoostMultiplier <= 1 ? 2 : u.BoostMultiplier;
                boostMultiplier = mult;
            }

            return baseScore * boostMultiplier;
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
