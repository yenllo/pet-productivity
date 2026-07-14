namespace PetProductivity.Client;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
        // Rutas push (no son pestañas): detalle de mascota de familia, tarea, stats, crear familia, registro.
        // Mascota personal (DashboardPage) y Ajustes (SettingsPage) ahora son pestañas del TabBar.
        Routing.RegisterRoute("PetDetailPage", typeof(Views.PetDetailPage));
        Routing.RegisterRoute("TaskPage", typeof(Views.TaskPage));
        Routing.RegisterRoute("StatsPage", typeof(Views.StatsPage));
        Routing.RegisterRoute("CreateGroupPage", typeof(Views.CreateGroupPage));
        Routing.RegisterRoute("RegisterPage", typeof(Views.RegisterPage));
        Routing.RegisterRoute("FocusAppsPage", typeof(Views.FocusAppsPage));
        Routing.RegisterRoute("FocusPage", typeof(Views.FocusPage));
        Routing.RegisterRoute("HistoryPage", typeof(Views.HistoryPage));
	}
}
