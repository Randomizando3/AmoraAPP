using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;
using Firebase.Auth;
using Firebase.Auth.Providers;

namespace AmoraApp.ViewModels
{
    public partial class AuthViewModel : ObservableObject
    {
        private readonly FirebaseAuthService _authService;
        private readonly FirebaseDatabaseService _dbService;

        // ===== Campos básicos =====
        [ObservableProperty] private string email;
        [ObservableProperty] private string password;
        [ObservableProperty] private string displayName;

        [ObservableProperty] private bool isBusy;

        [ObservableProperty] private string errorMessage;
        [ObservableProperty] private bool hasError;

        // ===== Assistente de registro (etapas) =====
        [ObservableProperty] private bool isStepName = true;
        [ObservableProperty] private bool isStepEmail;
        [ObservableProperty] private bool isStepCode;
        [ObservableProperty] private bool isStepProfile;

        [ObservableProperty] private string primaryButtonText = "Avançar";

        // Código de verificação
        [ObservableProperty] private string verificationCode;
        private string _generatedCode;

        // Foto de perfil (pré-cadastro)
        [ObservableProperty] private string photoUrl;
        private byte[] _photoBytes;

        // Flag interno para saber se o cadastro veio de login social (Google)
        private bool _isSocialSignUp = false;

        // Campos de perfil mínimos
        [ObservableProperty] private string bio;
        [ObservableProperty] private string city;
        [ObservableProperty] private int age = 18;
        [ObservableProperty] private string gender;
        [ObservableProperty] private string sexualOrientation;
        [ObservableProperty] private string religion;
        [ObservableProperty] private string phoneNumber;

        [ObservableProperty] private DateTime birthDate = DateTime.Today.AddYears(-18);
        public string AgeDisplay => $"{Age} anos";

        // Atualiza idade quando muda BirthDate
        partial void OnBirthDateChanged(DateTime oldValue, DateTime newValue)
        {
            var today = DateTime.Today;
            var age = today.Year - newValue.Year;
            if (newValue.Date > today.AddYears(-age))
                age--;

            if (age < 18) age = 18;
            Age = age;
        }

        partial void OnAgeChanged(int oldValue, int newValue)
        {
            OnPropertyChanged(nameof(AgeDisplay));
        }

        // Atualiza HasError sempre que ErrorMessage mudar
        partial void OnErrorMessageChanged(string value)
        {
            HasError = !string.IsNullOrWhiteSpace(value);
        }

        // ===== Itens selecionáveis (chips) =====
        public partial class SelectableItem : ObservableObject
        {
            public string Name { get; set; } = string.Empty;

            [ObservableProperty]
            private bool isSelected;
        }

        public ObservableCollection<SelectableItem> RelationshipGoals { get; } =
            new ObservableCollection<SelectableItem>();

        public ObservableCollection<SelectableItem> Interests { get; } =
            new ObservableCollection<SelectableItem>();

        public AuthViewModel()
            : this(FirebaseAuthService.Instance, FirebaseDatabaseService.Instance)
        {
        }

        public AuthViewModel(FirebaseAuthService authService, FirebaseDatabaseService dbService)
        {
            _authService = authService;
            _dbService = dbService;

            IsStepName = true;
            PrimaryButtonText = "Avançar";

            // Busco por
            RelationshipGoals.Add(new SelectableItem { Name = "Amizade" });
            RelationshipGoals.Add(new SelectableItem { Name = "Namoro" });
            RelationshipGoals.Add(new SelectableItem { Name = "Casamento" });
            RelationshipGoals.Add(new SelectableItem { Name = "Casual" });

            // Interesses padrão
            var defaultInterests = new[]
            {
                "Música", "Filmes e séries", "Viagens", "Games",
                "Esportes", "Pets", "Livros", "Gastronomia",
                "Tecnologia", "Arte", "Animes", "Baladas"
            };

            foreach (var name in defaultInterests)
                Interests.Add(new SelectableItem { Name = name });
        }

        // =========================================================
        // LOGIN EMAIL/SENHA
        // =========================================================
        [RelayCommand]
        private async Task LoginAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                {
                    ErrorMessage = "Preencha seu e-mail e sua senha.";
                    return;
                }

                var cred = await _authService.LoginWithEmailPasswordAsync(Email.Trim(), Password);
                var uid = cred.User.Uid;

                // UID salvo para auto-login
                Preferences.Set("auth_uid", uid);

                await PresenceService.Instance.SetOnlineAsync(uid);

                Application.Current.MainPage = new AppShell();
            }
            catch (FirebaseAuthException)
            {
                ErrorMessage = "Não foi possível entrar. Verifique seu e-mail e senha e tente novamente.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Erro inesperado ao entrar: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // =========================================================
        // FLUXO DE REGISTRO EM ETAPAS
        // =========================================================
        [RelayCommand]
        private async Task NextStepAsync()
        {
            if (IsBusy) return;
            ErrorMessage = string.Empty;

            // Etapa 1: Nome
            if (IsStepName)
            {
                if (string.IsNullOrWhiteSpace(DisplayName))
                {
                    ErrorMessage = "Qual é o seu nome?";
                    return;
                }

                IsStepName = false;
                IsStepEmail = true;
                IsStepCode = false;
                IsStepProfile = false;
                PrimaryButtonText = "Avançar";
                return;
            }

            // Etapa 2: E-mail + senha
            if (IsStepEmail)
            {
                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                {
                    ErrorMessage = "Informe um e-mail e uma senha.";
                    return;
                }

                if (Password.Length < 6)
                {
                    ErrorMessage = "A senha deve ter pelo menos 6 caracteres.";
                    return;
                }

                IsBusy = true;
                try
                {
                    var rnd = new Random();
                    _generatedCode = rnd.Next(100000, 999999).ToString();

                    await EmailService.Instance.SendVerificationCodeAsync(Email.Trim(), _generatedCode);

                    IsStepEmail = false;
                    IsStepCode = true;
                    PrimaryButtonText = "Verificar";
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Erro ao enviar o código: " + ex.Message;
                }
                finally
                {
                    IsBusy = false;
                }

                return;
            }

            // Etapa 3: Código
            if (IsStepCode)
            {
                if (string.IsNullOrWhiteSpace(VerificationCode))
                {
                    ErrorMessage = "Digite o código que você recebeu por e-mail.";
                    return;
                }

                if (VerificationCode.Trim() != _generatedCode)
                {
                    ErrorMessage = "Código inválido. Verifique o e-mail e tente novamente.";
                    return;
                }

                IsStepCode = false;
                IsStepProfile = true;
                PrimaryButtonText = "Concluir cadastro";
                return;
            }

            // Etapa 4: Perfil
            if (IsStepProfile)
            {
                await RegisterAsync();
            }
        }

        // =========================================================
        // FOTO DE PERFIL – CÂMERA OU GALERIA
        // =========================================================
        [RelayCommand]
        private async Task ChangePhotoAsync()
        {
            try
            {
                var page = Application.Current.MainPage;
                if (page == null) return;

                var action = await page.DisplayActionSheet(
                    "Foto de perfil",
                    "Cancelar",
                    null,
                    "Galeria",
                    "Câmera");

                if (action == "Galeria")
                {
                    var result = await FilePicker.PickAsync(new PickOptions
                    {
                        PickerTitle = "Escolha uma foto",
                        FileTypes = FilePickerFileType.Images
                    });

                    if (result == null)
                        return;

                    using var stream = await result.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    _photoBytes = ms.ToArray();

                    var ext = Path.GetExtension(result.FileName);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

                    var cachePath = Path.Combine(FileSystem.CacheDirectory,
                        $"register_profile_{Guid.NewGuid():N}{ext}");

                    File.WriteAllBytes(cachePath, _photoBytes);
                    PhotoUrl = cachePath;
                }
                else if (action == "Câmera")
                {
                    if (!MediaPicker.Default.IsCaptureSupported)
                    {
                        await page.DisplayAlert("Câmera indisponível",
                            "Este dispositivo não suporta captura de fotos.",
                            "OK");
                        return;
                    }

                    var photo = await MediaPicker.Default.CapturePhotoAsync();
                    if (photo == null)
                        return;

                    using var stream = await photo.OpenReadAsync();
                    using var ms = new MemoryStream();
                    await stream.CopyToAsync(ms);
                    _photoBytes = ms.ToArray();

                    var ext = Path.GetExtension(photo.FileName);
                    if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

                    var cachePath = Path.Combine(FileSystem.CacheDirectory,
                        $"register_profile_{Guid.NewGuid():N}{ext}");

                    File.WriteAllBytes(cachePath, _photoBytes);
                    PhotoUrl = cachePath;
                }
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Erro",
                    "Não foi possível selecionar/capturar a foto: " + ex.Message,
                    "OK");
            }
        }

        // =========================================================
        // TOGGLES DE PÍLULAS
        // =========================================================
        [RelayCommand]
        private void ToggleRelationshipGoal(SelectableItem item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
        }

        [RelayCommand]
        private void ToggleInterest(SelectableItem item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
        }

        // =========================================================
        // REGISTRO FINAL (EMAIL/SENHA ou SOCIAL)
        // =========================================================
        private async Task RegisterAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                if (Age < 18)
                {
                    ErrorMessage = "O app é apenas para maiores de 18 anos.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(City))
                {
                    ErrorMessage = "Informe sua cidade.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(Gender))
                {
                    ErrorMessage = "Selecione um gênero (pode ser 'Prefiro não dizer').";
                    return;
                }

                if (string.IsNullOrWhiteSpace(SexualOrientation))
                {
                    ErrorMessage = "Informe sua orientação (pode ser 'Prefiro não dizer').";
                    return;
                }

                var selectedGoals = RelationshipGoals
                    .Where(x => x.IsSelected)
                    .Select(x => x.Name)
                    .ToList();

                if (selectedGoals.Count == 0)
                {
                    ErrorMessage = "Marque ao menos uma opção do que você busca.";
                    return;
                }

                var selectedInterests = Interests
                    .Where(i => i.IsSelected)
                    .Select(i => i.Name)
                    .ToList();

                string uid;
                string email = Email?.Trim() ?? string.Empty;

                if (_isSocialSignUp)
                {
                    // Usuário já autenticado via Google
                    var currentUser = _authService.GetCurrentUser();
                    if (currentUser == null)
                    {
                        ErrorMessage = "Não foi possível continuar com o cadastro via Google. Tente novamente.";
                        return;
                    }

                    uid = currentUser.Uid;

                    if (string.IsNullOrWhiteSpace(email))
                        email = currentUser.Info.Email ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(DisplayName))
                        DisplayName = currentUser.Info.DisplayName ?? string.Empty;

                    Preferences.Set("auth_uid", uid);
                }
                else
                {
                    // Fluxo tradicional: criar usuário com email/senha
                    if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                    {
                        ErrorMessage = "Informe um e-mail e uma senha.";
                        return;
                    }

                    email = Email.Trim();

                    var cred = await _authService.RegisterWithEmailPasswordAsync(
                        email,
                        Password,
                        DisplayName.Trim());

                    uid = cred.User.Uid;

                    Preferences.Set("auth_uid", uid);
                }

                var profile = new UserProfile
                {
                    Id = uid,
                    DisplayName = DisplayName?.Trim() ?? string.Empty,
                    Email = email,
                    Bio = Bio?.Trim() ?? string.Empty,
                    City = City?.Trim() ?? string.Empty,
                    Age = Age,
                    Gender = Gender ?? string.Empty,
                    SexualOrientation = SexualOrientation ?? string.Empty,
                    Religion = Religion?.Trim() ?? string.Empty,
                    PhoneNumber = PhoneNumber?.Trim() ?? string.Empty,
                    LookingFor = selectedGoals,
                    Interests = selectedInterests,
                    EmailVerified = true // já validado (código) ou Google
                };

                var birthUtc = new DateTimeOffset(BirthDate.Date).ToUnixTimeMilliseconds();
                profile.BirthDateUtc = birthUtc;

                // Foto: se o usuário escolheu uma foto manual, sobe pro Storage.
                // Caso contrário, se veio do Google, usa PhotoUrl direto.
                if (_photoBytes != null && _photoBytes.Length > 0)
                {
                    using var ms = new MemoryStream(_photoBytes);
                    var path = $"users/{uid}/profile_{Guid.NewGuid():N}.jpg";
                    var url = await FirebaseStorageService.Instance.UploadImageAsync(ms, path);
                    profile.PhotoUrl = url;
                }
                else if (!string.IsNullOrWhiteSpace(PhotoUrl) &&
                         PhotoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    profile.PhotoUrl = PhotoUrl;
                }

                await _dbService.SaveUserProfileAsync(profile);

                await PresenceService.Instance.SetOnlineAsync(uid);

                Application.Current.MainPage = new AppShell();
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

        // =========================================================
        // Navegação login / register
        // =========================================================
        [RelayCommand]
        private async Task GoToRegisterAsync()
        {
            if (Application.Current.MainPage is NavigationPage nav)
                await nav.PushAsync(new Views.RegisterPage());
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
            if (Application.Current.MainPage is NavigationPage nav)
                await nav.PopAsync();
        }

        // =========================================================
        // LOGIN COM GOOGLE (via WebView / SignInWithRedirectAsync)
        // =========================================================
        /// <summary>
        /// Login com Google usando fluxo de redirect. O callback abre um WebView,
        /// espera o redirect para /__/auth/handler e devolve a URL final.
        /// </summary>
        public async Task LoginWithGoogleAsync(Func<Uri, Task<Uri>> openBrowserAndWaitForRedirectAsync)
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var client = _authService.Client;

                // Delegate agora recebe string (startUrl) e retorna string (finalUrl)
                var userCredential = await client.SignInWithRedirectAsync(
                    FirebaseProviderType.Google,
                    async startUrl =>
                    {
                        // Converte a string que o Firebase manda para Uri
                        var startUri = new Uri(startUrl);

                        // Usa o callback do LoginPage (que trabalha com Uri)
                        var finalUri = await openBrowserAndWaitForRedirectAsync(startUri);

                        // Devolve como string, que é o que o delegate espera
                        return finalUri.ToString();
                    });

                if (userCredential == null || userCredential.User == null)
                {
                    ErrorMessage = "Não foi possível autenticar com o Google.";
                    return;
                }

                var user = userCredential.User;
                var uid = user.Uid;
                var email = user.Info.Email ?? string.Empty;
                var name = user.Info.DisplayName ?? string.Empty;
                var photo = user.Info.PhotoUrl;

                Preferences.Set("auth_uid", uid);

                // Verifica se já existe perfil no banco
                var existingProfile = await _dbService.GetUserProfileAsync(uid);
                if (existingProfile != null)
                {
                    await PresenceService.Instance.SetOnlineAsync(uid);
                    Application.Current.MainPage = new AppShell();
                    return;
                }

                // Novo usuário via Google → ir para fluxo de criação de perfil
                _isSocialSignUp = true;

                DisplayName = name;
                Email = email;
                if (!string.IsNullOrEmpty(photo))
                    PhotoUrl = photo;

                // Pular etapas de nome / email+senha / código
                IsStepName = false;
                IsStepEmail = false;
                IsStepCode = false;
                IsStepProfile = true;
                PrimaryButtonText = "Concluir cadastro";

                // Abre a tela de cadastro com este mesmo ViewModel
                if (Application.Current.MainPage is NavigationPage nav)
                {
                    await nav.PushAsync(new Views.RegisterPage(this));
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Erro ao entrar com Google: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }


        // =========================================================
        // SOCIAL LOGIN APPLE (mantém placeholder)
        // =========================================================
        [RelayCommand]
        private async Task LoginWithAppleAsync()
        {
            await Application.Current.MainPage.DisplayAlert(
                "Login com Apple",
                "Login com Apple será configurado em uma próxima etapa.",
                "OK");
        }
    }
}
