using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace PetProductivity.Client;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseSkiaSharp()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				// Neo-Retro UI: Inter (cuerpo) + Plus Jakarta Sans (display).
				fonts.AddFont("Inter-Variable.ttf", "Inter");
				fonts.AddFont("PlusJakartaSans-SemiBold.ttf", "PlusJakarta");
				fonts.AddFont("PlusJakartaSans-Bold.ttf", "PlusJakartaBold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

        // Registrar HttpClient con BaseUrl dinámico + Bearer automático (AuthHeaderHandler).
        builder.Services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<PetProductivity.Client.Services.SettingsService>();
            var handler = new PetProductivity.Client.Services.AuthHeaderHandler(settings) { InnerHandler = new HttpClientHandler() };
            return new HttpClient(handler) { BaseAddress = new Uri(settings.ServerUrl) };
        });

        // Services
        // Services
        builder.Services.AddSingleton<PetProductivity.Client.Services.SettingsService>();
        builder.Services.AddSingleton<PetProductivity.Client.Services.AuthService>();
        builder.Services.AddSingleton<PetProductivity.Client.Services.GameDataService>();
        builder.Services.AddSingleton<PetProductivity.Client.Services.GroupService>();
        builder.Services.AddSingleton<PetProductivity.Client.Services.RealtimeService>();
        builder.Services.AddSingleton<PetProductivity.Client.Services.PushRegistration>();
#if ANDROID
        builder.Services.AddSingleton<PetProductivity.Client.Services.INotificationService, PetProductivity.Client.Platforms.Android.AndroidNotificationService>();
#else
        builder.Services.AddSingleton<PetProductivity.Client.Services.INotificationService, PetProductivity.Client.Services.NotificationService>();
#endif

        // Modo foco (AC3 v2): guardián por plataforma (Android bloquea; resto no-op) + sesión viva singleton.
#if ANDROID
        builder.Services.AddSingleton<PetProductivity.Client.Services.IFocusGuard, PetProductivity.Client.Platforms.Android.FocusGuard>();
#else
        builder.Services.AddSingleton<PetProductivity.Client.Services.IFocusGuard, PetProductivity.Client.Services.NoopFocusGuard>();
#endif
        builder.Services.AddSingleton<PetProductivity.Client.Services.FocusSessionService>();

        // Registrar ViewModels
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.BirthCeremonyViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.IntroViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.LoginViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.RegisterViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.DashboardViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.TaskViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.ShopViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.StatsViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.ProfileViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.SettingsViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.HubViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.PetDetailViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.CreateGroupViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.FocusAppsViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.FocusViewModel>();
        builder.Services.AddTransient<PetProductivity.Client.ViewModels.HistoryViewModel>();

        // Registrar Pages
        builder.Services.AddTransient<PetProductivity.Client.Views.BirthCeremonyPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.IntroPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.DashboardPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.TaskPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.ShopPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.StatsPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.ProfilePage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.LoginPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.RegisterPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.SettingsPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.HubPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.PetDetailPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.CreateGroupPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.FocusAppsPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.FocusPage>();
        builder.Services.AddTransient<PetProductivity.Client.Views.HistoryPage>();

		return builder.Build();
	}
}
