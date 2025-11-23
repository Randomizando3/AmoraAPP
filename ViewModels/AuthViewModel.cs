using System.Threading.Tasks;
using AmoraApp.Models;
using AmoraApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace AmoraApp.ViewModels
{
    public partial class AuthViewModel : ObservableObject
    {
        private readonly FirebaseAuthService _authService;
        private readonly FirebaseDatabaseService _dbService;

        [ObservableProperty]
        private string email;

        [ObservableProperty]
        private string password;

        [ObservableProperty]
        private string displayName;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string errorMessage;

        public AuthViewModel()
            : this(FirebaseAuthService.Instance, FirebaseDatabaseService.Instance)
        {
        }

        public AuthViewModel(FirebaseAuthService authService, FirebaseDatabaseService dbService)
        {
            _authService = authService;
            _dbService = dbService;
        }

        // LOGIN
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
                    ErrorMessage = "Preencha email e senha.";
                    return;
                }

                var cred = await _authService.LoginWithEmailPasswordAsync(Email.Trim(), Password);
                var uid = cred.User.Uid;

                // ✅ Marca como online
                await PresenceService.Instance.SetOnlineAsync(uid);

                // depois do login, joga para o AppShell
                Application.Current.MainPage = new AppShell();
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // REGISTRO
        [RelayCommand]
        private async Task RegisterAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(DisplayName))
                {
                    ErrorMessage = "Informe um nome.";
                    return;
                }

                if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
                {
                    ErrorMessage = "Preencha email e senha.";
                    return;
                }

                var email = Email.Trim();

                // Cria usuário no Firebase Auth
                var cred = await _authService.RegisterWithEmailPasswordAsync(email, Password, DisplayName.Trim());
                var uid = cred.User.Uid;

                // Cria perfil básico no Realtime Database
                var profile = new UserProfile
                {
                    Id = uid,
                    DisplayName = DisplayName.Trim(),
                    Email = email,
                    Bio = "",
                    City = "",
                    Age = 18,
                    Gender = "",
                    PhotoUrl = ""
                };

                await _dbService.SaveUserProfileAsync(profile);

                // ✅ Marca como online após registro
                await PresenceService.Instance.SetOnlineAsync(uid);

                // Vai direto para o AppShell
                Application.Current.MainPage = new AppShell();
            }
            catch (System.Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task GoToRegisterAsync()
        {
            if (Application.Current.MainPage is NavigationPage nav)
            {
                await nav.PushAsync(new Views.RegisterPage());
            }
        }

        [RelayCommand]
        private async Task GoToLoginAsync()
        {
            if (Application.Current.MainPage is NavigationPage nav)
            {
                await nav.PopAsync();
            }
        }
    }
}
