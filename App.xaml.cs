using AmoraApp.Services;
using AmoraApp.ViewModels;
using AmoraApp.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace AmoraApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // VM obrigatório para LoginPage
            var authVm = new AuthViewModel();

            var authService = FirebaseAuthService.Instance;

            // Fonte primária: FirebaseAuth (se o SDK ainda tiver sessão viva)
            var firebaseUid = authService.CurrentUserUid;

            // Fallback: Preferences (controle seu)
            var localUid = Preferences.Get("auth_uid", string.Empty);

            var uid = !string.IsNullOrWhiteSpace(firebaseUid)
                ? firebaseUid
                : localUid;

            if (!string.IsNullOrWhiteSpace(uid))
            {
                // Garante consistência
                Preferences.Set("auth_uid", uid);

                // 🔴 MUITO IMPORTANTE
                // Nunca persista admin localmente
                Preferences.Remove("is_admin");
                Preferences.Remove("admin_uid");

                MainPage = new AppShell();
            }
            else
            {
                // Sem sessão → login limpo
                Preferences.Remove("auth_uid");
                Preferences.Remove("is_admin");
                Preferences.Remove("admin_uid");

                MainPage = new NavigationPage(new LoginPage(authVm));
            }
        }
    }
}
