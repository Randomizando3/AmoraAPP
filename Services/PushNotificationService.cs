using System.Collections.Generic;
using Microsoft.Maui.Storage;

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

    public void HandleNotification(IDictionary<string, object> data)
    {
        // Extend here to route notification data to the UI or analytics
        if (data?.ContainsKey("message") == true)
        {
            var message = data["message"];
            System.Diagnostics.Debug.WriteLine($"Push message: {message}");
        }
    }
}
