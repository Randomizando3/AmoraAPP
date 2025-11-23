using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class RegisterPage : ContentPage
    {
        public RegisterPage()
        {
            InitializeComponent();
            // Reaproveita o mesmo AuthViewModel singleton
            BindingContext = new AuthViewModel(
                Services.FirebaseAuthService.Instance,
                Services.FirebaseDatabaseService.Instance);
        }
    }
}
