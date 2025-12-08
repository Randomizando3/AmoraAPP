using System;
using System.Threading.Tasks;
using AmoraApp.Config;
using AmoraApp.ViewModels;
using Microsoft.Maui.Controls;

namespace AmoraApp.Views
{
    public partial class LoginPage : ContentPage
    {
        private AuthViewModel Vm => BindingContext as AuthViewModel;

        // TCS usado para esperar o redirect no WebView
        private TaskCompletionSource<string> _navigationTcs;
        private string _targetUrl;

        public LoginPage(AuthViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;

            AuthWebView.Navigated += AuthWebView_Navigated;

            // UserAgent "mobile" para o Google não reclamar de user agent inválido
            AuthWebView.UserAgent =
                "Mozilla/5.0 (Linux; Android 8.0; Pixel 2 Build/OPD3.170816.012) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/93.0.4577.82 Mobile Safari/537.36";
        }

        // Clique no ícone do Google
        private async void OnGoogleTapped(object sender, EventArgs e)
        {
            if (Vm == null)
                return;

            var authDomain = FirebaseSettings.AuthDomain.TrimEnd('/');
            var redirectUrl = $"https://{authDomain}/__/auth/handler";

            try
            {
                await Vm.LoginWithGoogleAsync(async startUri =>
                {
                    // mostra overlay e configura TCS
                    _targetUrl = redirectUrl;
                    _navigationTcs = new TaskCompletionSource<string>();

                    AuthOverlay.IsVisible = true;
                    AuthWebView.Source = startUri;

                    // espera navegação até o handler do Firebase
                    var finalUrl = await WaitForNavigationToUrlAsync();

                    // esconde overlay
                    AuthOverlay.IsVisible = false;

                    return new Uri(finalUrl);
                });
            }
            catch (Exception ex)
            {
                AuthOverlay.IsVisible = false;
                await DisplayAlert("Erro", "Falha ao entrar com Google: " + ex.Message, "OK");
            }
        }

        // Botão "Fechar" do overlay
        private void OnCloseAuthClicked(object sender, EventArgs e)
        {
            AuthOverlay.IsVisible = false;

            if (_navigationTcs != null && !_navigationTcs.Task.IsCompleted)
            {
                _navigationTcs.TrySetException(new OperationCanceledException("Login cancelado pelo usuário."));
            }
        }

        private Task<string> WaitForNavigationToUrlAsync()
        {
            return _navigationTcs.Task;
        }

        private void AuthWebView_Navigated(object sender, WebNavigatedEventArgs e)
        {
            if (_navigationTcs == null || _navigationTcs.Task.IsCompleted)
                return;

            if (!string.IsNullOrEmpty(_targetUrl) &&
                !string.IsNullOrEmpty(e.Url) &&
                e.Url.StartsWith(_targetUrl, StringComparison.OrdinalIgnoreCase))
            {
                _navigationTcs.TrySetResult(e.Url);
            }
        }
    }
}
