using AmoraApp.Views;
using AmoraApp.Services;
using Microsoft.Maui.Controls;

namespace AmoraApp
{
    public partial class App : Application
    {
        public App(WelcomePage welcomePage)
        {
            InitializeComponent();

            // Garante que o serviço de auth é inicializado
            var auth = FirebaseAuthService.Instance;

            // Se já existir sessão do Firebase, vai direto para o AppShell
            if (!string.IsNullOrEmpty(auth.CurrentUserUid))
            {
                // Usuário já está autenticado (Firebase persiste a sessão)
                MainPage = new AppShell();
            }
            else
            {
                // Ninguém logado: cai na tela de boas-vindas (Login / Register)
                MainPage = new NavigationPage(welcomePage);
            }
        }
    }
}
