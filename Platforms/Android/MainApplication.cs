using Android.App;
using Android.Runtime;
using Plugin.FirebasePushNotification;

namespace AmoraApp
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override void OnCreate()
        {
            base.OnCreate();

            FirebasePushNotificationManager.DefaultNotificationChannelId = "general";
            FirebasePushNotificationManager.DefaultNotificationChannelName = "General";

            FirebasePushNotificationManager.Initialize(this, true);
        }
    }
}
