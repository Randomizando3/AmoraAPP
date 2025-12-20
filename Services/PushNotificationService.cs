using Microsoft.Maui.Storage;
using Plugin.FirebasePushNotification.Abstractions;

namespace AmoraApp.Services;

public class PushNotificationService
{
    private const string TokenKey = "PushNotificationToken";

    public string? Token { get; private set; }

    public PushNotificationService()
    {
        Token = Preferences.Get(TokenKey, null);
    }

    public void UpdateToken(string token)
    {
        Token = token;
        Preferences.Set(TokenKey, token);
    }

    public void HandleNotification(FirebasePushNotificationDataEventArgs notification)
    {
        // Extend here to route notification data to the UI or analytics
        if (notification.Data?.ContainsKey("message") == true)
        {
            var message = notification.Data["message"];
            System.Diagnostics.Debug.WriteLine($"Push message: {message}");
        }
    }
}
