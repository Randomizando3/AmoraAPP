using AmoraApp.Services;
using AmoraApp.ViewModels;
using AmoraApp.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Plugin.Maui.Audio;
using Plugin.FirebasePushNotifications;
using Microsoft.Maui.LifecycleEvents;

#if WINDOWS
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;
#endif

#if ANDROID
using Android.Webkit;
using Microsoft.Maui.Handlers;
#endif

namespace AmoraApp
{
    public static class MauiProgram
    {
        public static IServiceProvider ServiceProvider { get; private set; }

#if ANDROID
        // Handler customizado para liberar autoplay e file:// no Android WebView
        private sealed class CustomWebViewHandler : WebViewHandler
        {
            protected override void ConnectHandler(Android.Webkit.WebView platformView)
            {
                base.ConnectHandler(platformView);

                try
                {
                    var s = platformView.Settings;

                    s.JavaScriptEnabled = true;

                    // PERMITE autoplay sem toque (resolve o "triângulo de play")
                    s.MediaPlaybackRequiresUserGesture = false;

                    // Permite carregar file:// no <video> source
                    s.AllowFileAccess = true;
                    s.AllowContentAccess = true;

                    // Permite file:// acessar file:// e outras origens (precisa para alguns devices)
                    s.AllowFileAccessFromFileURLs = true;
                    s.AllowUniversalAccessFromFileURLs = true;

                    // Evita bloqueio por mixed content (mais “permissivo”)
                    s.MixedContentMode = MixedContentHandling.AlwaysAllow;

                    // Mantém playback inline
                    // (Android WebView já tende a usar inline; isso ajuda em alguns aparelhos)
                    platformView.SetWebChromeClient(new WebChromeClient());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[CustomWebViewHandler] erro: " + ex);
                }
            }
        }
#endif

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseFirebasePushNotifications()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if ANDROID
            // Aplica handler custom no Android
            builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler(typeof(Microsoft.Maui.Controls.WebView), typeof(CustomWebViewHandler));
            });
#endif

            // Plugin.Maui.Audio
            builder.Services.AddSingleton(AudioManager.Current);

            // SERVICES SINGLETONS
            builder.Services.AddSingleton<FirebaseAuthService>(_ => FirebaseAuthService.Instance);
            builder.Services.AddSingleton<FirebaseDatabaseService>(_ => FirebaseDatabaseService.Instance);

            // VIEWMODELS
            builder.Services.AddTransient<AuthViewModel>();
            builder.Services.AddTransient<FeedViewModel>();
            builder.Services.AddTransient<DiscoverViewModel>();
            builder.Services.AddTransient<MessagesViewModel>();
            builder.Services.AddTransient<ChatViewModel>();
            builder.Services.AddTransient<ProfileViewModel>();
            builder.Services.AddTransient<LikesViewModel>();

            // PAGES
            builder.Services.AddTransient<WelcomePage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();

            builder.Services.AddTransient<FeedPage>();
            builder.Services.AddTransient<DiscoverPage>();
            builder.Services.AddTransient<MessagesPage>();
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<ProfilePage>();
            builder.Services.AddTransient<LikesReceivedPage>();

#if WINDOWS
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddWindows(w =>
                {
                    w.OnWindowCreated(window =>
                    {
                        const int width = 390;
                        const int height = 760;

                        try
                        {
                            var winuiWindow = (Microsoft.UI.Xaml.Window)window;
                            var hWnd = WindowNative.GetWindowHandle(winuiWindow);
                            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                            var appWindow = AppWindow.GetFromWindowId(windowId);

                            if (appWindow != null)
                            {
                                appWindow.Resize(new SizeInt32(width, height));

                                if (appWindow.Presenter is OverlappedPresenter presenter)
                                {
                                    presenter.IsResizable = false;
                                    presenter.IsMaximizable = false;
                                    presenter.IsMinimizable = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Erro ao definir janela: " + ex);
                        }
                    });
                });
            });
#endif

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();
            ServiceProvider = app.Services;

            _ = PushNotificationBootstrapper.InitializeAsync();

            return app;
        }
    }
}
