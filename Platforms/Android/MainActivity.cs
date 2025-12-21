using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using System;

namespace AmoraApp
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        Exported = true,
        LaunchMode = LaunchMode.SingleTask,
        ConfigurationChanges =
            ConfigChanges.ScreenSize |
            ConfigChanges.Orientation |
            ConfigChanges.UiMode |
            ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize |
            ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        private const int RequestPostNotificationsCode = 20101;

        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            try
            {
                // Android 8+: canal de notificação
                AmoraApp.Services.PushNotificationBootstrapper.EnsureAndroidNotificationChannel();

                // Android 13+: runtime permission para notificações
                RequestPostNotificationsPermissionIfNeeded();

                // Inicializa FCM (idempotente)
                _ = AmoraApp.Services.PushNotificationBootstrapper.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] OnCreate error: {ex}");
            }
        }

        private void RequestPostNotificationsPermissionIfNeeded()
        {
            try
            {
                if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu)
                    return;

                var granted = ContextCompat.CheckSelfPermission(this, Manifest.Permission.PostNotifications) == Permission.Granted;
                if (granted)
                    return;

                ActivityCompat.RequestPermissions(
                    this,
                    new[] { Manifest.Permission.PostNotifications },
                    RequestPostNotificationsCode
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] RequestPostNotificationsPermissionIfNeeded error: {ex}");
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            // Importante para MAUI Essentials/Permissions
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
