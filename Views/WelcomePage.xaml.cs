using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class WelcomePage : ContentPage
    {
        public WelcomePage()
        {
            InitializeComponent();
        }

        private async void OnLoginClicked(object sender, System.EventArgs e)
        {
            await Navigation.PushAsync(
                MauiProgram.ServiceProvider.GetService<LoginPage>()
                ?? new LoginPage(null!)); // nunca chega aqui, só pra evitar null
        }

        private async void OnRegisterClicked(object sender, System.EventArgs e)
        {
            await Navigation.PushAsync(
                MauiProgram.ServiceProvider.GetService<RegisterPage>()
                ?? new RegisterPage());
        }
    }
}
