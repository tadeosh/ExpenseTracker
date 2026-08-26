using Android.App;
using Android.Content.PM;
using Android.OS;

namespace ExpenseTracker
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // TO JEST JEDYNY KOD, KTÓREGO POTRZEBUJESZ NA ANDROIDZIE
        protected override void OnSaveInstanceState(Bundle outState)
        {
            // Ratuje aplikację przed JavaProxyThrowable w .NET MAUI
            outState?.Clear();
            base.OnSaveInstanceState(outState);
        }

    }
}
