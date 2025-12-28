using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AmoraApp.Views
{
    public partial class WelcomePage : ContentPage
    {
        private bool _initialized;

        public WelcomePage()
        {
            InitializeComponent();
            BgWeb.Navigated += OnWebNavigated;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            if (_initialized) return;
            _initialized = true;

            try
            {
                // 1) Garante o MP4 no AppData (funciona bem em Android e Windows)
                var videoFilePath = await EnsureWelcomeVideoFileAsync();
                var videoUri = new Uri(videoFilePath).AbsoluteUri; // file:///...

                // 2) Gera um HTML físico (file:///.../welcome_bg.html) e navega por URL
                //    (WebView2 é MUITO mais estável assim do que HtmlWebViewSource)
                var htmlFilePath = await EnsureWelcomeHtmlFileAsync(videoUri);
                var htmlUri = new Uri(htmlFilePath).AbsoluteUri;

                BgWeb.Source = new UrlWebViewSource { Url = htmlUri };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[WelcomePage] Falha ao preparar vídeo/html: " + ex);

                BgWeb.Source = new HtmlWebViewSource
                {
                    Html = "<html><body style='margin:0;background:#000;'></body></html>"
                };
            }
        }

        private async void OnWebNavigated(object sender, WebNavigatedEventArgs e)
        {
            try
            {
                // Força play depois que a navegação terminou (WebView2 às vezes precisa disso)
                await Task.Delay(120);
                await BgWeb.EvaluateJavaScriptAsync("window.__tryPlay && window.__tryPlay();");

                await Task.Delay(350);
                await BgWeb.EvaluateJavaScriptAsync("window.__tryPlay && window.__tryPlay();");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[WelcomePage] JS play falhou: " + ex);
            }
        }

        private static async Task<string> EnsureWelcomeVideoFileAsync()
        {
            const string fileName = "welcome.mp4";
            var targetPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            if (File.Exists(targetPath))
                return targetPath;

            Directory.CreateDirectory(FileSystem.AppDataDirectory);

            using var sourceStream = await FileSystem.OpenAppPackageFileAsync(fileName);
            using var targetStream = File.OpenWrite(targetPath);
            await sourceStream.CopyToAsync(targetStream);

            return targetPath;
        }

        private static async Task<string> EnsureWelcomeHtmlFileAsync(string videoUri)
        {
            const string htmlName = "welcome_bg.html";
            var htmlPath = Path.Combine(FileSystem.AppDataDirectory, htmlName);

            // Regera sempre (para evitar cache do WebView2 com html antigo)
            // Se quiser, pode colocar um if(File.Exists) return htmlPath;
            Directory.CreateDirectory(FileSystem.AppDataDirectory);

            // HTML SEM interpolação com { } (evita quebrar o C#)
            var html = @"
<!doctype html>
<html>
<head>
  <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0'/>
  <style>
    html, body {
      margin:0; padding:0; width:100%; height:100%;
      background:#000; overflow:hidden;
    }
    video {
      position:fixed;
      top:50%; left:50%;
      transform:translate(-50%,-50%);
      min-width:100%;
      min-height:100%;
      width:auto;
      height:auto;
      object-fit:cover;
      background:#000;
    }
  </style>
</head>
<body>
  <video id='v' muted playsinline preload='auto' autoplay>
    <source src='__SRC__' type='video/mp4' />
  </video>

  <script>
    const v = document.getElementById('v');

    window.__tryPlay = function() {
      try {
        const p = v.play();
        if (p && p.catch) p.catch(() => {});
      } catch(e) {}
    };

    // Tenta várias vezes
    setTimeout(window.__tryPlay, 50);
    setTimeout(window.__tryPlay, 250);
    setTimeout(window.__tryPlay, 700);

    v.addEventListener('loadeddata', () => setTimeout(window.__tryPlay, 10));
    v.addEventListener('canplay', () => setTimeout(window.__tryPlay, 10));

    // 1x e para no final
    v.addEventListener('ended', () => {
      try {
        v.pause();
        v.currentTime = Math.max(0, v.duration - 0.02);
      } catch(e) {}
    });

    // Debug básico (se der erro, não fica só preto)
    v.addEventListener('error', () => {
      try {
        document.body.style.background = '#000';
      } catch(e) {}
    });
  </script>
</body>
</html>";

            html = html.Replace("__SRC__", videoUri);

            await File.WriteAllTextAsync(htmlPath, html);
            return htmlPath;
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(
                MauiProgram.ServiceProvider.GetService<LoginPage>()
                ?? new LoginPage(null!));
        }

        private async void OnRegisterClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(
                MauiProgram.ServiceProvider.GetService<RegisterPage>()
                ?? new RegisterPage());
        }
    }
}
