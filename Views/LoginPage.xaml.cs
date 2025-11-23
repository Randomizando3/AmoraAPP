using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class LoginPage : ContentPage
    {
        public LoginPage(AuthViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }
    }
}
