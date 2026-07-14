namespace PetProductivity.Client;

public partial class App : Application
{
    // Ir a Ajustes del sistema a conceder un permiso (Acceso de uso / Superponer) y volver es un resume
    // de Activity de Android, NO navegación de Shell: Page.OnAppearing no se vuelve a disparar, así que
    // la fila de permisos quedaba con el estado de antes hasta cerrar y reabrir la app entera. OnResume
    // sí se dispara con cualquier "app al frente", venga de donde venga.
    public event EventHandler? Resumed;
    protected override void OnResume()
    {
        base.OnResume();
        Resumed?.Invoke(this, EventArgs.Empty);
    }

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
        var gameData = serviceProvider.GetRequiredService<Services.GameDataService>();
        gameData.StartOfflineQueue();

        // Precalentar la tienda en segundo plano (fuera del hilo de UI): cuando el usuario toque la pestaña,
        // el catálogo y los sprites ya están en RAM/disco y la abre al instante. No bloquea el arranque: si
        // no hay red o aún no hay sesión, falla en silencio y la tienda lo reintenta al abrirse.
        _ = Task.Run(async () =>
        {
            try { await ViewModels.ShopViewModel.EnsureSpriteCacheAsync(await gameData.GetCatalogAsync()); }
            catch { }
        });

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

            // Cubre el caso EN CALIENTE (app ya viva): tocar la notificación/overlay de foco tras haber
            // navegado al menú principal. El restore de arriba solo corre al crear la App (arranque en
            // frío); esto es lo que faltaba para volver a FocusPage sin matar la app primero.
            focus.Guard.ReopenRequested += (_, _) =>
            {
                if (!focus.IsActive) return; // la sesión ya terminó entre postear la notificación y tocarla
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try { await Shell.Current.GoToAsync("FocusPage"); } catch { }
                });
            };
        }
	}
}
