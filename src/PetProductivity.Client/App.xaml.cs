namespace PetProductivity.Client;

public partial class App : Application
{
	public App(IServiceProvider serviceProvider)
	{
		Services.L.Init(); // #26: resolver el idioma ANTES de inflar cualquier página
		InitializeComponent();

        // Dark-first: el look Neo-Retro es oscuro. Solo se usa claro si el usuario lo eligió
        // explícitamente (evita navbar/Shell en blanco cuando el sistema está en tema claro).
        var theme = Preferences.Get("AppTheme", "Dark");
        Application.Current.UserAppTheme = theme == "Light" ? AppTheme.Light : AppTheme.Dark;
        Services.ThemeService.Apply(dark: theme != "Light"); // #25: paleta clara suave (antes solo cambiaba el Shell)

        // Al completar un foco, FocusSessionService dispara notificación local + vibración (funciona en
        // segundo plano); la página del foco muestra el mensaje inline. No hace falta alert global.

        // T13: ping de calentamiento (cold start de Render) + drenar la cola offline pendiente;
        // queda suscrito a Connectivity para drenar solo al volver la señal.
        serviceProvider.GetRequiredService<Services.GameDataService>().StartOfflineQueue();

        if (!Preferences.Get("HasHatched", false))
        {
            MainPage = new NavigationPage(serviceProvider.GetRequiredService<Views.BirthCeremonyPage>());
        }
        else
        {
            MainPage = new AppShell();

            // Si había un foco activo (cerraron la app desde recientes), reanudarlo y volver a su pantalla.
            var focus = serviceProvider.GetRequiredService<Services.FocusSessionService>();
            if (focus.TryRestore())
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(600); // dar tiempo a que el Shell esté listo
                    try { await Shell.Current.GoToAsync("FocusPage"); } catch { }
                });
        }
	}
}
