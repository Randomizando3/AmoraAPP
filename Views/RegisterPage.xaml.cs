using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class RegisterPage : ContentPage
    {
        // Construtor padrão (usado quando vem do botão "Criar nova conta")
        public RegisterPage()
        {
            InitializeComponent();
            // Cria um novo AuthViewModel (fluxo de cadastro tradicional)
            BindingContext = new AuthViewModel(
                Services.FirebaseAuthService.Instance,
                Services.FirebaseDatabaseService.Instance);
        }

        // Construtor extra para reusar o MESMO AuthViewModel (ex.: login com Google)
        public RegisterPage(AuthViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
