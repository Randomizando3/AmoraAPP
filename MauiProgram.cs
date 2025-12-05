using AmoraApp.Services;
using AmoraApp.ViewModels;
using AmoraApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.LifecycleEvents;
using Plugin.Maui.Audio;

#if WINDOWS
using Microsoft.UI.Windowing;          // AppWindow, OverlappedPresenter
using Windows.Graphics;                // SizeInt32
using WinRT.Interop;                   // WindowNative.GetWindowHandle
#endif

namespace AmoraApp
{
    public static class MauiProgram
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Plugin.Maui.Audio
            builder.Services.AddSingleton(AudioManager.Current);

            // ---------------------------
            // CONFIGURAÇÃO DE JANELA FIXA NO WINDOWS
            // ---------------------------
            builder.ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
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
#endif
            });

            // ---------------------------
            // SERVICES SINGLETONS
            // ---------------------------
            builder.Services.AddSingleton<FirebaseAuthService>(_ => FirebaseAuthService.Instance);
            builder.Services.AddSingleton<FirebaseDatabaseService>(_ => FirebaseDatabaseService.Instance);

            // ---------------------------
            // VIEWMODELS
            // ---------------------------
            builder.Services.AddTransient<AuthViewModel>();
            builder.Services.AddTransient<FeedViewModel>();
            builder.Services.AddTransient<DiscoverViewModel>();
            builder.Services.AddTransient<MessagesViewModel>();
            builder.Services.AddTransient<ProfileViewModel>();

            // ---------------------------
            // VIEWS
            // ---------------------------
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<WelcomePage>();
            builder.Services.AddTransient<DiscoverPage>();
            builder.Services.AddTransient<FeedPage>();
            builder.Services.AddTransient<ChatListPage>();
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<ProfilePage>();
            builder.Services.AddTransient<FiltersPage>();
            builder.Services.AddTransient<PhotoGalleryPage>();

            var app = builder.Build();
            ServiceProvider = app.Services;

            return app;
        }
    }
}
