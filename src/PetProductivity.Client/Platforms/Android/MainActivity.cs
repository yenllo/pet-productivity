using Android.App;
using Android.Content.PM;
using Android.OS;

namespace PetProductivity.Client;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Inicializa Firebase (para push FCM). Si falla, no debe tumbar la app.
        // Plugin.Firebase 4.x: Initialize ahora pide un activityLocator (Func<Activity>).
        try { Plugin.Firebase.Core.Platforms.Android.CrossFirebase.Initialize(this, () => Microsoft.Maui.ApplicationModel.Platform.CurrentActivity!); }
        catch (System.Exception ex) { System.Console.WriteLine($"Firebase init failed: {ex.Message}"); }

        Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            var path = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal), "crash.txt");
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, args.Exception.ToString());
        };
    }
}
