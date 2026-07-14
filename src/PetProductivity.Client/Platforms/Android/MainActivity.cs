using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using PetProductivity.Client.Platforms.Android;

namespace PetProductivity.Client;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // SingleTop: si la app ya está viva, Android reusa esta instancia y llama aquí en vez de recrearla.
    // Sin esto, tocar la notificación de foco (o su overlay "Volver a PetProductivity") con la app ya
    // abierta solo traía la app al frente donde el Shell la hubiera dejado (p. ej. el Dashboard),
    // nunca de vuelta a FocusPage — el restore de App.xaml.cs solo corre en arranque en frío.
    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        if (intent?.Action == FocusGuardService.ActionOpenFocus)
            FocusGuard.RaiseReopenRequested();
    }

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
