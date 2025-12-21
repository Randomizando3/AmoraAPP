using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Dispatching;
using Plugin.FirebasePushNotifications;

namespace AmoraApp.Services
{
    public static class PushNotificationBootstrapper
    {
        public const string AndroidDefaultChannelId = "amora_default";

        private static int _initialized;
        private static string? _lastToken;
        private static string? _lastSavedTokenForUid;

        public static async Task InitializeAsync()
        {
            if (Interlocked.Exchange(ref _initialized, 1) == 1)
                return;

            try
            {
#if ANDROID
                EnsureAndroidNotificationChannel();
#endif
                var push = IFirebasePushNotification.Current;

                push.TokenRefreshed += (s, e) =>
                {
                    _ = HandleTokenAsync(e.Token);
                };

                push.NotificationReceived += (s, e) =>
                {
                    Debug.WriteLine($"[FCM] NotificationReceived: {SafeSerialize(e.Data)}");
                };

                push.NotificationOpened += (s, e) =>
                {
                    Debug.WriteLine($"[FCM] NotificationOpened: {SafeSerialize(e.Data)}");
                    _ = HandleOpenedAsync(e.Data);
                };

                // Registra no FCM
                await push.RegisterForPushNotificationsAsync();

                // Alguns devices já têm token imediatamente
                if (!string.IsNullOrWhiteSpace(push.Token))
                    await HandleTokenAsync(push.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] Initialize failed: {ex}");
            }
        }

        /// <summary>
        /// Chame após login/registro (ou ao abrir com sessão já logada).
        /// Garante que token atual esteja salvo para o uid.
        /// </summary>
        public static async Task BindCurrentUserAsync(string uid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(uid))
                    return;

                if (Volatile.Read(ref _initialized) == 0)
                    await InitializeAsync();

                var push = IFirebasePushNotification.Current;

                var token = !string.IsNullOrWhiteSpace(push.Token) ? push.Token : _lastToken;
                if (string.IsNullOrWhiteSpace(token))
                {
                    Debug.WriteLine("[FCM] BindCurrentUserAsync: token ainda indisponível.");
                    return;
                }

                await PersistTokenAsync(uid, token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] BindCurrentUserAsync failed: {ex}");
            }
        }

        private static async Task HandleTokenAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return;

            _lastToken = token;

            try
            {
                var uid = FirebaseAuthService.Instance.CurrentUserUid;

                if (!string.IsNullOrWhiteSpace(uid))
                    await PersistTokenAsync(uid, token);

                Debug.WriteLine($"[FCM] Token: {token}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] HandleTokenAsync failed: {ex}");
            }
        }

        private static async Task PersistTokenAsync(string uid, string token)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
                    return;

                // Dedup simples (evita escrita repetida)
                var key = $"{uid}:{token}";
                if (string.Equals(_lastSavedTokenForUid, key, StringComparison.Ordinal))
                    return;

                var platform = DeviceInfo.Platform.ToString().ToLowerInvariant(); // android / ios / winui
                await FirebaseDatabaseService.Instance.SavePushTokenAsync(uid, platform, token);

                _lastSavedTokenForUid = key;

                Debug.WriteLine($"[FCM] Token persisted uid={uid}, platform={platform}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] PersistTokenAsync failed: {ex}");
            }
        }

        private static Task HandleOpenedAsync(IDictionary<string, object> data)
        {
            try
            {
                if (data == null || data.Count == 0)
                    return Task.CompletedTask;

                if (data.TryGetValue("type", out var typeObj))
                {
                    var type = (typeObj?.ToString() ?? "").Trim().ToLowerInvariant();
                    Preferences.Set("push_last_type", type);

                    if (type == "message" && data.TryGetValue("chatId", out var chatIdObj))
                        Preferences.Set("push_last_chatId", chatIdObj?.ToString() ?? "");

                    if (data.TryGetValue("fromUid", out var fromUidObj))
                        Preferences.Set("push_last_fromUid", fromUidObj?.ToString() ?? "");
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Aqui você pode disparar navegação se quiser, com segurança (ex.: MessagingCenter / event aggregator).
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] HandleOpenedAsync failed: {ex}");
            }

            return Task.CompletedTask;
        }

#if ANDROID
        public static void EnsureAndroidNotificationChannel()
        {
            try
            {
                if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.O)
                    return;

                var channel = new Android.App.NotificationChannel(
                    AndroidDefaultChannelId,
                    "AmoraApp",
                    Android.App.NotificationImportance.Default)
                {
                    Description = "Curtidas, matches e mensagens"
                };

                var manager = (Android.App.NotificationManager?)Android.App.Application.Context
                    .GetSystemService(Android.Content.Context.NotificationService);

                manager?.CreateNotificationChannel(channel);

                Debug.WriteLine($"[FCM] NotificationChannel ensured: {AndroidDefaultChannelId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FCM] EnsureAndroidNotificationChannel failed: {ex}");
            }
        }
#else
        public static void EnsureAndroidNotificationChannel() { }
#endif

        private static string SafeSerialize(IDictionary<string, object>? data)
        {
            try
            {
                if (data == null) return "{}";
                var parts = new List<string>();
                foreach (var kv in data)
                    parts.Add($"{kv.Key}={kv.Value}");
                return string.Join(", ", parts);
            }
            catch
            {
                return "{?}";
            }
        }
    }
}
