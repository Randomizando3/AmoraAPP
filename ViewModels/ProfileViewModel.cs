using AmoraApp.Models;
using AmoraApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AmoraApp.ViewModels
{
    public class PhotoSlot : ObservableObject
    {
        private int _index;
        public int Index { get => _index; set => SetProperty(ref _index, value); }

        private string? _imageUrl;
        public string? ImageUrl
        {
            get => _imageUrl;
            set
            {
                if (SetProperty(ref _imageUrl, value))
                    OnPropertyChanged(nameof(ShowPlus));
            }
        }

        public bool ShowPlus => string.IsNullOrWhiteSpace(ImageUrl);
    }

    public class VideoSlot : ObservableObject
    {
        private int _index;
        public int Index { get => _index; set => SetProperty(ref _index, value); }

        private string? _videoUrl;
        public string? VideoUrl
        {
            get => _videoUrl;
            set
            {
                if (SetProperty(ref _videoUrl, value))
                {
                    OnPropertyChanged(nameof(ShowPlus));
                    OnPropertyChanged(nameof(HasVideo));
                }
            }
        }

        public bool ShowPlus => string.IsNullOrWhiteSpace(VideoUrl);
        public bool HasVideo => !string.IsNullOrWhiteSpace(VideoUrl);
    }

    public class InterestItem : ObservableObject
    {
        private string _name = "";
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        public InterestItem() { }
        public InterestItem(string name, bool selected = false)
        {
            Name = name;
            IsSelected = selected;
        }
    }

    public class ProfileViewModel : ObservableObject
    {
        private readonly FirebaseAuthService _authService;
        private readonly FirebaseDatabaseService _dbService;

        private const int MaxPhotos = 30;
        private const int MaxVideos = 20;

        public ProfileViewModel() : this(FirebaseAuthService.Instance, FirebaseDatabaseService.Instance) { }

        public ProfileViewModel(FirebaseAuthService authService, FirebaseDatabaseService dbService)
        {
            _authService = authService;
            _dbService = dbService;

            ToggleInterestCommand = new Command<InterestItem>(item =>
            {
                if (item == null) return;
                item.IsSelected = !item.IsSelected;
            });

            ToggleRelationshipGoalCommand = new Command<InterestItem>(item =>
            {
                if (item == null) return;
                item.IsSelected = !item.IsSelected;
            });

            InitPhotoSlots();
            InitVideoSlots();
            InitInterests(null);
            InitRelationshipGoals(null);
        }

        // ===== Backing profile (fonte única) =====
        private UserProfile _profile = new();
        public UserProfile Profile
        {
            get => _profile;
            private set => SetProperty(ref _profile, value);
        }

        public string CurrentUserId { get; set; } = string.Empty;

        // ===== Propriedades bindáveis (1:1 com UserProfile) =====
        public string DisplayName
        {
            get => Profile.DisplayName;
            set { if (Profile.DisplayName != value) { Profile.DisplayName = value ?? ""; OnPropertyChanged(); } }
        }

        public string Email
        {
            get => Profile.Email;
            set { if (Profile.Email != value) { Profile.Email = value ?? ""; OnPropertyChanged(); } }
        }

        public string Bio
        {
            get => Profile.Bio;
            set { if (Profile.Bio != value) { Profile.Bio = value ?? ""; OnPropertyChanged(); } }
        }

        public string JobTitle
        {
            get => Profile.JobTitle;
            set { if (Profile.JobTitle != value) { Profile.JobTitle = value ?? ""; OnPropertyChanged(); } }
        }

        public string EducationLevel
        {
            get => Profile.EducationLevel;
            set { if (Profile.EducationLevel != value) { Profile.EducationLevel = value ?? ""; OnPropertyChanged(); } }
        }

        public string EducationInstitution
        {
            get => Profile.EducationInstitution;
            set { if (Profile.EducationInstitution != value) { Profile.EducationInstitution = value ?? ""; OnPropertyChanged(); } }
        }

        public string City
        {
            get => Profile.City;
            set { if (Profile.City != value) { Profile.City = value ?? ""; OnPropertyChanged(); } }
        }

        public string PhoneNumber
        {
            get => Profile.PhoneNumber;
            set { if (Profile.PhoneNumber != value) { Profile.PhoneNumber = value ?? ""; OnPropertyChanged(); } }
        }

        public string Gender
        {
            get => Profile.Gender;
            set { if (Profile.Gender != value) { Profile.Gender = value ?? ""; OnPropertyChanged(); } }
        }

        public string SexualOrientation
        {
            get => Profile.SexualOrientation;
            set { if (Profile.SexualOrientation != value) { Profile.SexualOrientation = value ?? ""; OnPropertyChanged(); } }
        }

        public string Religion
        {
            get => Profile.Religion;
            set { if (Profile.Religion != value) { Profile.Religion = value ?? ""; OnPropertyChanged(); } }
        }

        public string PhotoUrl
        {
            get => Profile.PhotoUrl;
            set { if (Profile.PhotoUrl != value) { Profile.PhotoUrl = value ?? ""; OnPropertyChanged(); } }
        }

        public string Plan
        {
            get => Profile.Plan;
            set
            {
                var v = string.IsNullOrWhiteSpace(value) ? "Free" : value;
                if (Profile.Plan != v)
                {
                    Profile.Plan = v;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PhotoLimit));
                    OnPropertyChanged(nameof(PhotosSectionTitle));
                }
            }
        }

        public double Latitude
        {
            get => Profile.Latitude;
            set { if (Profile.Latitude != value) { Profile.Latitude = value; OnPropertyChanged(); } }
        }

        public double Longitude
        {
            get => Profile.Longitude;
            set { if (Profile.Longitude != value) { Profile.Longitude = value; OnPropertyChanged(); } }
        }

        public string CurrentLocationText
        {
            get => Profile.CurrentLocationText;
            set { if (Profile.CurrentLocationText != value) { Profile.CurrentLocationText = value ?? ""; OnPropertyChanged(); } }
        }

        // ===== Admin / Verificação (do seu UserProfile) =====
        private bool _isAdmin;
        public bool IsAdmin { get => _isAdmin; private set => SetProperty(ref _isAdmin, value); }

        public bool IsVerified
        {
            get => Profile.IsVerified;
            set
            {
                if (Profile.IsVerified != value)
                {
                    Profile.IsVerified = value;
                    OnPropertyChanged();
                    RaiseVerificationComputed();
                }
            }
        }

        public string VerificationStatus
        {
            get => Profile.VerificationStatus;
            set
            {
                var v = NormalizeStatus(value);
                if (Profile.VerificationStatus != v)
                {
                    Profile.VerificationStatus = v;
                    OnPropertyChanged();
                    RaiseVerificationComputed();
                }
            }
        }

        private void RaiseVerificationComputed()
        {
            OnPropertyChanged(nameof(CanRequestVerification));
            OnPropertyChanged(nameof(ShowVerificationStatus));
            OnPropertyChanged(nameof(VerificationStatusLabel));
        }

        public bool CanRequestVerification =>
            !IsVerified && !string.Equals(VerificationStatus, "pending", StringComparison.OrdinalIgnoreCase);

        public bool ShowVerificationStatus =>
            !IsVerified &&
            (string.Equals(VerificationStatus, "pending", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(VerificationStatus, "rejected", StringComparison.OrdinalIgnoreCase));

        public string VerificationStatusLabel
        {
            get
            {
                if (string.Equals(VerificationStatus, "pending", StringComparison.OrdinalIgnoreCase))
                    return "Verificação pendente. Aguarde a análise.";
                if (string.Equals(VerificationStatus, "rejected", StringComparison.OrdinalIgnoreCase))
                    return "Verificação rejeitada. Você pode solicitar novamente.";
                return "";
            }
        }

        private static string NormalizeStatus(string? raw)
        {
            var s = (raw ?? "").Trim().ToLowerInvariant();
            return s switch
            {
                "approved" => "approved",
                "pending" => "pending",
                "rejected" => "rejected",
                "none" => "none",
                "" => "none",
                _ => s
            };
        }

        // ===== Estado =====
        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }

        // ===== Slots / Lists =====
        public ObservableCollection<PhotoSlot> ExtraPhotoSlots { get; } = new();
        public ObservableCollection<VideoSlot> ExtraVideoSlots { get; } = new();

        public ObservableCollection<InterestItem> Interests { get; } = new();
        public ObservableCollection<InterestItem> RelationshipGoals { get; } = new();

        public ObservableCollection<string> JobSuggestions { get; } = new();
        private bool _isJobSuggestionsVisible;
        public bool IsJobSuggestionsVisible { get => _isJobSuggestionsVisible; set => SetProperty(ref _isJobSuggestionsVisible, value); }

        public ICommand ToggleInterestCommand { get; }
        public ICommand ToggleRelationshipGoalCommand { get; }

        public int PhotoLimit => GetPhotoLimitForPlan(Plan);
        public string PhotosSectionTitle => $"Fotos (até {PhotoLimit})";

        private static int GetPhotoLimitForPlan(string? plan)
        {
            plan = (plan ?? "Free").Trim();
            if (plan.Equals("Premium", StringComparison.OrdinalIgnoreCase)) return 30;
            if (plan.Equals("Plus", StringComparison.OrdinalIgnoreCase)) return 15;
            return 5;
        }

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

        private static readonly string[] DefaultInterests =
        {
            "Música","Filmes","Séries","Viagem","Games","Pets",
            "Gastronomia","Esportes","Livros","Tecnologia",
            "Arte","Natureza","Praia","Balada","Café"
        };

        private static readonly string[] DefaultRelationshipGoals =
        {
            "Amizade","Namoro","Casamento","Casual"
        };

        private void InitPhotoSlots()
        {
            ExtraPhotoSlots.Clear();
            for (int i = 0; i < MaxPhotos; i++)
                ExtraPhotoSlots.Add(new PhotoSlot { Index = i, ImageUrl = null });
        }

        private void InitVideoSlots()
        {
            ExtraVideoSlots.Clear();
            for (int i = 0; i < MaxVideos; i++)
                ExtraVideoSlots.Add(new VideoSlot { Index = i, VideoUrl = null });
        }

        private void ApplyPhotosToSlots(List<string>? photos)
        {
            InitPhotoSlots();
            photos ??= new List<string>();
            for (int i = 0; i < Math.Min(MaxPhotos, photos.Count); i++)
                ExtraPhotoSlots[i].ImageUrl = photos[i];
        }

        private void ApplyVideosToSlots(List<string>? videos)
        {
            InitVideoSlots();
            videos ??= new List<string>();
            for (int i = 0; i < Math.Min(MaxVideos, videos.Count); i++)
                ExtraVideoSlots[i].VideoUrl = videos[i];
        }

        private List<string> BuildPhotosFromSlots()
        {
            var all = ExtraPhotoSlots
                .Where(s => !string.IsNullOrWhiteSpace(s.ImageUrl))
                .Select(s => s.ImageUrl!.Trim())
                .ToList();

            return all.Take(Math.Max(0, PhotoLimit)).ToList();
        }

        private List<string> BuildVideosFromSlots()
        {
            return ExtraVideoSlots
                .Where(s => !string.IsNullOrWhiteSpace(s.VideoUrl))
                .Select(s => s.VideoUrl!.Trim())
                .ToList();
        }

        private void InitInterests(IEnumerable<string>? selected)
        {
            Interests.Clear();
            var set = new HashSet<string>(selected ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var name in DefaultInterests)
                Interests.Add(new InterestItem(name, set.Contains(name)));
        }

        private void InitRelationshipGoals(IEnumerable<string>? selected)
        {
            RelationshipGoals.Clear();
            var set = new HashSet<string>(selected ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var name in DefaultRelationshipGoals)
                RelationshipGoals.Add(new InterestItem(name, set.Contains(name)));
        }

        private List<string> GetSelectedInterests() =>
            Interests.Where(i => i.IsSelected).Select(i => i.Name).ToList();

        private List<string> GetSelectedRelationshipGoals() =>
            RelationshipGoals.Where(i => i.IsSelected).Select(i => i.Name).ToList();

        public void OnJobTextChanged(string text)
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

        // ===== Load / Save =====
        public async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = "";

            try
            {
                var user = _authService.GetCurrentUser();
                if (user == null)
                {
                    ErrorMessage = "Sessão expirada. Faça login novamente.";
                    return;
                }

                var uid = user.Uid;
                CurrentUserId = uid;
                IsAdmin = AdminAccessService.IsAdmin(uid);


                var profile = await _dbService.GetUserProfileAsync(uid);

                if (profile == null)
                {
                    var authUser = _authService.GetCurrentUser();
                    profile = new UserProfile
                    {
                        Id = uid,
                        DisplayName = authUser?.Info.DisplayName ?? "",
                        Email = authUser?.Info.Email ?? "",
                        Age = 18
                    };

                    await _dbService.SaveUserProfileAsync(profile);
                }

                Profile = profile;

                // garantir defaults
                if (string.IsNullOrWhiteSpace(Profile.Plan)) Profile.Plan = "Free";
                Profile.VerificationStatus = NormalizeStatus(Profile.VerificationStatus);

                // aplicar slots/listas do próprio model
                var photos = (Profile.Photos != null && Profile.Photos.Count > 0)
                    ? Profile.Photos
                    : (Profile.Gallery ?? new List<string>());

                ApplyPhotosToSlots(photos);

                ApplyVideosToSlots(Profile.Videos ?? new List<string>());

                InitInterests(Profile.Interests ?? new List<string>());
                InitRelationshipGoals(Profile.LookingFor ?? new List<string>());

                // notificar bindings do “wrapper”
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(Email));
                OnPropertyChanged(nameof(Bio));
                OnPropertyChanged(nameof(JobTitle));
                OnPropertyChanged(nameof(EducationLevel));
                OnPropertyChanged(nameof(EducationInstitution));
                OnPropertyChanged(nameof(City));
                OnPropertyChanged(nameof(PhoneNumber));
                OnPropertyChanged(nameof(Gender));
                OnPropertyChanged(nameof(SexualOrientation));
                OnPropertyChanged(nameof(Religion));
                OnPropertyChanged(nameof(PhotoUrl));
                OnPropertyChanged(nameof(Plan));
                OnPropertyChanged(nameof(Latitude));
                OnPropertyChanged(nameof(Longitude));
                OnPropertyChanged(nameof(CurrentLocationText));
                OnPropertyChanged(nameof(IsVerified));
                OnPropertyChanged(nameof(VerificationStatus));
                RaiseVerificationComputed();
                OnPropertyChanged(nameof(PhotoLimit));
                OnPropertyChanged(nameof(PhotosSectionTitle));
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

        public async Task SaveAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = "";

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

                // sincroniza slots -> Profile
                var photos = BuildPhotosFromSlots();
                var videos = BuildVideosFromSlots();

                Profile.Id = uid;

                Profile.Photos = photos;
                Profile.Gallery = photos; // legado
                Profile.Videos = videos;

                Profile.Interests = GetSelectedInterests();
                Profile.LookingFor = GetSelectedRelationshipGoals();

                if (string.IsNullOrWhiteSpace(Profile.Plan)) Profile.Plan = "Free";
                Profile.VerificationStatus = NormalizeStatus(Profile.VerificationStatus);

                await _dbService.SaveUserProfileAsync(Profile);
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

        public async Task UpdateLocationAsync()
        {
            try
            {
                var result = await LocationService.Instance.GetCurrentLocationAsync();
                if (result == null) return;

                Latitude = result.Latitude;
                Longitude = result.Longitude;
                CurrentLocationText = string.IsNullOrWhiteSpace(result.Description)
                    ? $"{result.Latitude:0.0000}, {result.Longitude:0.0000}"
                    : result.Description;

                await SaveAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
