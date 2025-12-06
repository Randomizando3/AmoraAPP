using AmoraApp.Models;
using AmoraApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace AmoraApp.ViewModels
{
    // Slot individual de foto (até 30)
    public partial class PhotoSlot : ObservableObject
    {
        [ObservableProperty] private int index;
        [ObservableProperty] private string imageUrl;

        // usado no XAML para mostrar o "+" quando vazio
        public bool ShowPlus => string.IsNullOrWhiteSpace(ImageUrl);

        partial void OnImageUrlChanged(string value)
        {
            OnPropertyChanged(nameof(ShowPlus));
        }
    }

    // Slot individual de vídeo (até 20)
    public partial class VideoSlot : ObservableObject
    {
        [ObservableProperty] private int index;
        [ObservableProperty] private string videoUrl;

        public bool ShowPlus => string.IsNullOrWhiteSpace(VideoUrl);
        public bool HasVideo => !string.IsNullOrWhiteSpace(VideoUrl);

        partial void OnVideoUrlChanged(string value)
        {
            OnPropertyChanged(nameof(ShowPlus));
            OnPropertyChanged(nameof(HasVideo));
        }
    }

    // Chip genérico (interesse / "busco por")
    public partial class InterestItem : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private bool isSelected;

        public InterestItem() { }

        public InterestItem(string name, bool selected = false)
        {
            this.name = name;
            isSelected = selected;
        }
    }

    public partial class ProfileViewModel : ObservableObject
    {
        private readonly FirebaseAuthService _authService;
        private readonly FirebaseDatabaseService _dbService;

        private const int MaxPhotos = 30;
        private const int MaxVideos = 20;

        // Campos básicos
        [ObservableProperty] private string displayName;
        [ObservableProperty] private string email;
        [ObservableProperty] private string bio;

        // Profissão
        [ObservableProperty] private string jobTitle;

        // Escolaridade (nível) + instituição
        [ObservableProperty] private string educationLevel;
        [ObservableProperty] private string educationInstitution;

        [ObservableProperty] private string city;

        // Telefone / celular
        [ObservableProperty] private string phoneNumber;

        // Gênero
        [ObservableProperty] private string gender;

        // Orientação sexual
        [ObservableProperty] private string sexualOrientation;

        // Religião
        [ObservableProperty] private string religion;

        [ObservableProperty] private string photoUrl;

        // Plano atual ("Free", "Plus", "Premium")
        [ObservableProperty] private string plan = "Free";

        // Data de nascimento
        [ObservableProperty] private DateTime? birthDate;

        // ===== Localização =====
        [ObservableProperty] private double latitude;
        [ObservableProperty] private double longitude;
        [ObservableProperty] private string currentLocationText = "Localização ainda não capturada";

        // Slots de fotos (até 30)
        [ObservableProperty] private ObservableCollection<PhotoSlot> extraPhotoSlots = new();

        // Slots de vídeos (até 20)
        [ObservableProperty] private ObservableCollection<VideoSlot> extraVideoSlots = new();

        // Interesses
        [ObservableProperty] private ObservableCollection<InterestItem> interests = new();

        // "Busco por" (amizade, namoro, etc.)
        [ObservableProperty] private ObservableCollection<InterestItem> relationshipGoals = new();

        // Autocomplete de profissões
        [ObservableProperty] private ObservableCollection<string> jobSuggestions = new();
        [ObservableProperty] private bool isJobSuggestionsVisible;

        // Estado
        [ObservableProperty] private bool isBusy;
        [ObservableProperty] private string errorMessage;

        public string CurrentUserId { get; set; } = string.Empty;

        // Sugestões de profissões
        private readonly List<string> _allJobTitles = new()
        {
            "Desenvolvedor de Software",
            "Programador C#",
            "Programador .NET",
            "Desenvolvedor Mobile",
            "Designer Gráfico",
            "Ilustrador",
            "Animador 2D",
            "Editor de Vídeo",
            "Professor",
            "Estudante",
            "Engenheiro",
            "Arquiteto",
            "Médico",
            "Enfermeiro",
            "Psicólogo",
            "Advogado",
            "Vendedor",
            "Atendente",
            "Analista de Sistemas",
            "Analista de Suporte",
            "Gestor de Projetos",
            "Empreendedor",
            "Autônomo",
            "Freelancer"
        };

        // Interesses padrão
        private static readonly string[] DefaultInterests =
        {
            "Música","Filmes","Séries","Viagem","Games","Pets",
            "Gastronomia","Esportes","Livros","Tecnologia",
            "Arte","Natureza","Praia","Balada","Café"
        };

        // Opções do "Busco por"
        private static readonly string[] DefaultRelationshipGoals =
        {
            "Amizade","Namoro","Casamento","Casual"
        };

        // Opções de escolaridade / gênero / orientação sexual
        public IList<string> EducationLevelOptions { get; } = new List<string>
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

        public IList<string> GenderOptions { get; } = new List<string>
        {
            "Homem",
            "Mulher",
            "Homem trans",
            "Mulher trans",
            "Não-binário",
            "Outro",
            "Prefiro não dizer"
        };

        public IList<string> SexualOrientationOptions { get; } = new List<string>
        {
            "Heterossexual",
            "Homossexual",
            "Bissexual",
            "Pansexual",
            "Assexual",
            "Prefiro não dizer"
        };

        // Exibição da idade (derivada da BirthDate)
        public string AgeDisplay
        {
            get
            {
                if (!BirthDate.HasValue)
                    return "Não informado";

                var age = CalculateAgeFromDate(BirthDate.Value);
                if (age <= 0) return "Não informado";
                return $"{age} anos";
            }
        }

        partial void OnBirthDateChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(AgeDisplay));
        }

        public ProfileViewModel()
            : this(FirebaseAuthService.Instance, FirebaseDatabaseService.Instance)
        {
        }

        public ProfileViewModel(FirebaseAuthService authService, FirebaseDatabaseService dbService)
        {
            _authService = authService;
            _dbService = dbService;

            InitPhotoSlots();
            InitVideoSlots();
            InitInterests(null);
            InitRelationshipGoals(null);
        }

        #region Inicialização slots

        private void InitPhotoSlots()
        {
            ExtraPhotoSlots.Clear();
            for (int i = 0; i < MaxPhotos; i++)
            {
                ExtraPhotoSlots.Add(new PhotoSlot
                {
                    Index = i,
                    ImageUrl = null
                });
            }
        }

        private void InitVideoSlots()
        {
            ExtraVideoSlots.Clear();
            for (int i = 0; i < MaxVideos; i++)
            {
                ExtraVideoSlots.Add(new VideoSlot
                {
                    Index = i,
                    VideoUrl = null
                });
            }
        }

        private void ApplyPhotosToSlots(IList<string> photos)
        {
            InitPhotoSlots();

            if (photos == null)
                return;

            int limit = Math.Min(MaxPhotos, photos.Count);
            for (int i = 0; i < limit; i++)
            {
                ExtraPhotoSlots[i].ImageUrl = photos[i];
            }
        }

        private void ApplyVideosToSlots(IList<string> videos)
        {
            InitVideoSlots();

            if (videos == null)
                return;

            int limit = Math.Min(MaxVideos, videos.Count);
            for (int i = 0; i < limit; i++)
            {
                ExtraVideoSlots[i].VideoUrl = videos[i];
            }
        }

        private List<string> BuildPhotosFromSlots()
        {
            return ExtraPhotoSlots
                .Where(s => !string.IsNullOrWhiteSpace(s.ImageUrl))
                .Select(s => s.ImageUrl)
                .ToList();
        }

        private List<string> BuildVideosFromSlots()
        {
            return ExtraVideoSlots
                .Where(s => !string.IsNullOrWhiteSpace(s.VideoUrl))
                .Select(s => s.VideoUrl)
                .ToList();
        }

        #endregion

        #region Interesses

        private void InitInterests(IEnumerable<string> selected)
        {
            Interests.Clear();

            var selectedSet = new HashSet<string>(
                selected ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in DefaultInterests)
            {
                Interests.Add(new InterestItem(name, selectedSet.Contains(name)));
            }
        }

        [RelayCommand]
        private void ToggleInterest(InterestItem item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
        }

        private List<string> GetSelectedInterests()
        {
            return Interests
                .Where(i => i.IsSelected)
                .Select(i => i.Name)
                .ToList();
        }

        #endregion

        #region "Busco por" (relationship goals)

        private void InitRelationshipGoals(IEnumerable<string> selected)
        {
            RelationshipGoals.Clear();

            var selectedSet = new HashSet<string>(
                selected ?? Array.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in DefaultRelationshipGoals)
            {
                RelationshipGoals.Add(new InterestItem(name, selectedSet.Contains(name)));
            }
        }

        [RelayCommand]
        private void ToggleRelationshipGoal(InterestItem item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
        }

        private List<string> GetSelectedRelationshipGoals()
        {
            return RelationshipGoals
                .Where(i => i.IsSelected)
                .Select(i => i.Name)
                .ToList();
        }

        #endregion

        #region Profissão (autocomplete)

        public void OnJobTextChanged(string text)
        {
            UpdateJobSuggestions(text);
        }

        private void UpdateJobSuggestions(string text)
        {
            JobSuggestions.Clear();

            if (string.IsNullOrWhiteSpace(text))
            {
                IsJobSuggestionsVisible = false;
                return;
            }

            var term = text.Trim().ToLowerInvariant();

            var matches = _allJobTitles
                .Where(j => j.ToLowerInvariant().Contains(term))
                .OrderBy(j => j)
                .Take(40)
                .ToList();

            foreach (var j in matches)
                JobSuggestions.Add(j);

            IsJobSuggestionsVisible = JobSuggestions.Count > 0;
        }

        #endregion

        #region Idade (cálculo)

        private int CalculateAgeFromDate(DateTime birthDate)
        {
            var today = DateTime.UtcNow.Date;
            var b = birthDate.Date;

            int age = today.Year - b.Year;
            if (b > today.AddYears(-age))
                age--;

            if (age < 0) age = 0;
            if (age > 120) age = 120;

            return age;
        }

        #endregion

        #region Load / Save

        public async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var uid = _authService.CurrentUserUid;
                if (string.IsNullOrEmpty(uid))
                {
                    ErrorMessage = "Usuário não autenticado.";
                    return;
                }

                CurrentUserId = uid;

                var profile = await _dbService.GetUserProfileAsync(uid);

                if (profile == null)
                {
                    var authUser = _authService.GetCurrentUser();
                    profile = new UserProfile
                    {
                        Id = uid,
                        DisplayName = authUser?.Info.DisplayName ?? string.Empty,
                        Email = authUser?.Info.Email ?? string.Empty,
                        Age = 18
                    };

                    await _dbService.SaveUserProfileAsync(profile);
                }

                DisplayName = profile.DisplayName;
                Email = profile.Email;
                Bio = profile.Bio;
                JobTitle = profile.JobTitle;
                EducationLevel = profile.EducationLevel;
                EducationInstitution = profile.EducationInstitution;
                City = profile.City;
                PhoneNumber = profile.PhoneNumber;
                Gender = profile.Gender;
                SexualOrientation = profile.SexualOrientation;
                Religion = profile.Religion;
                PhotoUrl = profile.PhotoUrl;

                // Plano
                Plan = string.IsNullOrWhiteSpace(profile.Plan) ? "Free" : profile.Plan;

                Latitude = profile.Latitude;
                Longitude = profile.Longitude;
                CurrentLocationText = string.IsNullOrWhiteSpace(profile.CurrentLocationText)
                    ? "Localização ainda não capturada"
                    : profile.CurrentLocationText;

                // Data de nascimento / idade
                DateTime? birth = null;
                if (profile.BirthDateUtc > 0)
                {
                    birth = DateTimeOffset
                        .FromUnixTimeMilliseconds(profile.BirthDateUtc)
                        .UtcDateTime
                        .Date;
                }
                else if (profile.Age > 0)
                {
                    // fallback aproximado pra dados antigos
                    birth = DateTime.UtcNow.AddYears(-profile.Age).Date;
                }

                BirthDate = birth;

                // Fotos: migração de Gallery → Photos se necessário
                var photos = (profile.Photos != null && profile.Photos.Count > 0)
                    ? profile.Photos
                    : (profile.Gallery ?? new List<string>());

                ApplyPhotosToSlots(photos);

                // Vídeos
                var videos = profile.Videos ?? new List<string>();
                ApplyVideosToSlots(videos);

                InitInterests(profile.Interests ?? new List<string>());
                InitRelationshipGoals(profile.LookingFor ?? new List<string>());
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task SaveAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var uid = CurrentUserId;
                if (string.IsNullOrEmpty(uid))
                {
                    uid = _authService.CurrentUserUid;
                    if (string.IsNullOrEmpty(uid))
                    {
                        ErrorMessage = "Usuário não autenticado.";
                        return;
                    }

                    CurrentUserId = uid;
                }

                int ageYears = 0;
                long birthUtc = 0;

                if (BirthDate.HasValue)
                {
                    var b = BirthDate.Value.Date;
                    ageYears = CalculateAgeFromDate(b);

                    var bUtc = DateTime.SpecifyKind(b, DateTimeKind.Utc);
                    birthUtc = new DateTimeOffset(bUtc).ToUnixTimeMilliseconds();
                }

                var photos = BuildPhotosFromSlots();
                var videos = BuildVideosFromSlots();

                // Carrega o perfil existente para preservar plano, boosts etc.
                var existing = await _dbService.GetUserProfileAsync(uid) ?? new UserProfile
                {
                    Id = uid
                };

                existing.DisplayName = DisplayName ?? "";
                existing.Email = Email ?? "";
                existing.Bio = Bio ?? "";
                existing.JobTitle = JobTitle ?? "";
                existing.EducationLevel = EducationLevel ?? "";
                existing.EducationInstitution = EducationInstitution ?? "";
                existing.City = City ?? "";
                existing.PhoneNumber = PhoneNumber ?? "";
                existing.Gender = Gender ?? "";
                existing.SexualOrientation = SexualOrientation ?? "";
                existing.Religion = Religion ?? "";
                existing.PhotoUrl = PhotoUrl ?? "";

                existing.Age = ageYears;
                existing.BirthDateUtc = birthUtc;

                existing.Photos = photos;
                existing.Videos = videos;
                existing.Gallery = photos; // compat

                existing.Interests = GetSelectedInterests();
                existing.LookingFor = GetSelectedRelationshipGoals();

                existing.Latitude = Latitude;
                existing.Longitude = Longitude;
                existing.CurrentLocationText = CurrentLocationText ?? "";

                // Mantém o plano atual (se o ViewModel tiver algo setado, atualiza)
                existing.Plan = string.IsNullOrWhiteSpace(Plan) ? existing.Plan : Plan;

                await _dbService.SaveUserProfileAsync(existing);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RefreshAsync() => await LoadAsync();

        #endregion

        #region Localização (UpdateLocationAsync)

        public async Task UpdateLocationAsync()
        {
            try
            {
                var result = await LocationService.Instance.GetCurrentLocationAsync();
                if (result == null)
                    return;

                Latitude = result.Latitude;
                Longitude = result.Longitude;
                CurrentLocationText = string.IsNullOrWhiteSpace(result.Description)
                    ? $"{result.Latitude:0.0000}, {result.Longitude:0.0000}"
                    : result.Description;

                // Salva no perfil para ser usado depois no Discover
                await SaveAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        #endregion
    }
}
