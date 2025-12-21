using Foundation;
using Plugin.FirebasePushNotification;
using UIKit;
using UserNotifications;

namespace AmoraApp
{
    [Register("AppDelegate")]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            FirebasePushNotificationManager.Initialize(launchOptions, true);

            UNUserNotificationCenter.Current.RequestAuthorization(UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound, (approved, _) =>
            {
                if (approved)
                {
                    InvokeOnMainThread(UIApplication.SharedApplication.RegisterForRemoteNotifications);
                }
            });

            UNUserNotificationCenter.Current.Delegate = FirebasePushNotificationManager.CurrentNotificationDelegate;

            FirebasePushNotificationManager.CurrentNotificationPresentationOption = UNNotificationPresentationOptions.Alert | UNNotificationPresentationOptions.Sound | UNNotificationPresentationOptions.Badge;

            return base.FinishedLaunching(application, launchOptions);
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
