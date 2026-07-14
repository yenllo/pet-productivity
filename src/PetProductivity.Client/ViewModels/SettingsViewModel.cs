using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Microsoft.Maui.Controls;
using Microsoft.Maui.ApplicationModel;

using PetProductivity.Client.Services;

namespace PetProductivity.Client.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isDarkTheme;

        [ObservableProperty]
        private bool notificationsEnabled;

        [ObservableProperty]
        private bool remindDailyRitual;

        // T27-L2 (#28): la config de servidor/IA ahora vive colapsada en "Desarrollador".
        [ObservableProperty]
        private bool showDeveloper;

        [RelayCommand]
        private void ToggleDeveloper() => ShowDeveloper = !ShowDeveloper;

        private readonly HttpClient _httpClient;
        private readonly INotificationService _notificationService;
        private readonly SettingsService _settingsService;
        private readonly GameDataService _gameDataService;
        private readonly IFocusGuard _focusGuard;
        private readonly AuthService _authService;
        private readonly IServiceProvider _serviceProvider;

        // === Modo foco (AC3 v2) ===
        [ObservableProperty] private bool focusSupported;
        [ObservableProperty] private string usageStatus = string.Empty;
        [ObservableProperty] private Color usageColor = Colors.Gray;
        [ObservableProperty] private string overlayStatus = string.Empty;
        [ObservableProperty] private Color overlayColor = Colors.Gray;
        [ObservableProperty] private string notifStatus = string.Empty;
        [ObservableProperty] private Color notifColor = Colors.Gray;
        [ObservableProperty] private string focusAppsSummary = string.Empty;
        [ObservableProperty] private int focusGoalMinutes = 60;
        [ObservableProperty] private bool photoRewardsEnabled = true;

        private static Color OkColor => Colors.MediumSeaGreen;
        private static Color MissColor => Colors.Orange;

        [ObservableProperty]
        private string serverUrl = string.Empty;

        [ObservableProperty]
        private string connectionStatus = "Sin probar";

        [ObservableProperty]
        private Color connectionStatusColor = Colors.Gray;

        public SettingsViewModel(HttpClient httpClient, INotificationService notificationService, SettingsService settingsService, GameDataService gameDataService, IFocusGuard focusGuard, AuthService authService, IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _notificationService = notificationService;
            _settingsService = settingsService;
            _gameDataService = gameDataService;
            _focusGuard = focusGuard;
            _authService = authService;
            _serviceProvider = serviceProvider;
            LoadSettings();
            _ = RefreshFocus();
        }

        // Estado de cada permiso del modo foco por separado (se llama al volver a la pantalla).
        public async Task RefreshFocus()
        {
            FocusGoalMinutes = Preferences.Get(FocusSessionService.GoalKey, 60);
            PhotoRewardsEnabled = Preferences.Get(FocusSessionService.PhotoRewardsKey, true);
            FocusSupported = _focusGuard.IsSupported;
            var n = (Preferences.Get(FocusSessionService.AllowedAppsKey, "") ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
            FocusAppsSummary = L.F("{0}/3 apps permitidas", n);
            if (!FocusSupported) return;

            SetRow(_focusGuard.HasUsageAccess, s => UsageStatus = s, c => UsageColor = c);
            SetRow(_focusGuard.HasOverlay, s => OverlayStatus = s, c => OverlayColor = c);

            var notif = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            SetRow(notif == PermissionStatus.Granted, s => NotifStatus = s, c => NotifColor = c);
        }

        private static void SetRow(bool ok, Action<string> setStatus, Action<Color> setColor)
        {
            setStatus(ok ? L.T("Concedido ✅") : L.T("Falta ❌"));
            setColor(ok ? OkColor : MissColor);
        }

        partial void OnFocusGoalMinutesChanged(int value)
        {
            var clamped = Math.Clamp(value, 5, 480);
            if (clamped != value) { FocusGoalMinutes = clamped; return; }
            Preferences.Set(FocusSessionService.GoalKey, clamped);
        }

        partial void OnPhotoRewardsEnabledChanged(bool value) =>
            Preferences.Set(FocusSessionService.PhotoRewardsKey, value);

        [RelayCommand]
        private void GrantUsageAccess() => _focusGuard.RequestUsageAccess();

        [RelayCommand]
        private void GrantOverlay() => _focusGuard.RequestOverlay();

        [RelayCommand]
        private async Task GrantNotifications()
        {
            await Permissions.RequestAsync<Permissions.PostNotifications>();
            await RefreshFocus();
        }

        [RelayCommand]
        private async Task OpenFocusApps() => await Shell.Current.GoToAsync("FocusAppsPage");

        // #25: LoadSettings ASIGNA IsDarkTheme → dispararía OnIsDarkThemeChanged → rebuild del Shell
        // en plena creación de la página (loop infinito de AppShell + NRE del toolbar). La bandera
        // hace que el partial method solo reaccione a cambios DEL USUARIO.
        private bool _initializing;

        private void LoadSettings()
        {
            _initializing = true;
            // Technical
            ServerUrl = _settingsService.ServerUrl;

            // Theme
            var theme = Preferences.Get("AppTheme", "System");
            IsDarkTheme = theme == "Dark"; // Simple User toggle for now
            if (theme == "System")
            {
               IsDarkTheme = App.Current?.RequestedTheme == AppTheme.Dark;
            }

            // Notifications
            NotificationsEnabled = Preferences.Get("NotificationsEnabled", true);
            RemindDailyRitual = Preferences.Get("RemindDailyRitual", false);
            _initializing = false;
        }

        partial void OnRemindDailyRitualChanged(bool value) => Preferences.Set("RemindDailyRitual", value);

        partial void OnServerUrlChanged(string value)
        {
             _settingsService.ServerUrl = value;
             ConnectionStatus = L.T("Requiere Reinicio");
             ConnectionStatusColor = Colors.Orange;
        }

        [RelayCommand]
        private async Task TestConnection()
        {
            ConnectionStatus = L.T("Probando...");
            ConnectionStatusColor = Colors.Yellow;
            
            try 
            {
                // We construct a temporary client because the injected one might have the OLD url
                using var tempClient = new HttpClient { BaseAddress = new Uri(ServerUrl), Timeout = TimeSpan.FromSeconds(5) };
                // Endpoint anónimo (catalog): prueba de vida sin necesitar token.
                var response = await tempClient.GetAsync("api/shop/catalog");

                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                     ConnectionStatus = L.T("Conectado ✅");
                     ConnectionStatusColor = Colors.Green;
                }
                else
                {
                     ConnectionStatus = $"Error: {response.StatusCode}";
                     ConnectionStatusColor = Colors.Red;
                }
            }
            catch(Exception ex)
            {
                ConnectionStatus = L.T("Fallo ❌");
                ConnectionStatusColor = Colors.Red;
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        partial void OnIsDarkThemeChanged(bool value)
        {
            if (_initializing) return; // solo cambios del usuario (ver LoadSettings)
            var newTheme = value ? AppTheme.Dark : AppTheme.Light;
            if (App.Current != null)
            {
                App.Current.UserAppTheme = newTheme;
            }
            Preferences.Set("AppTheme", value ? "Dark" : "Light");
            // #25: aplicar la paleta clara/oscura y reconstruir el Shell para que las páginas
            // re-resuelvan sus StaticResource; se vuelve a Ajustes para no perder el contexto.
            ThemeService.Apply(dark: value);
            RebuildShellToSettings();
            SyncPreferences();
        }

        // T27-L3 (#26): idioma Sistema → Español → English (tocar cicla). Reconstruye el Shell
        // para que las páginas re-evalúen {loc:T} — mismo truco que el tema.
        [ObservableProperty]
        private string languageLabel = CurrentLangLabel();

        private static string CurrentLangLabel() => Preferences.Get("AppLang", "system") switch
        {
            "es" => "Español",
            "en" => "English",
            _ => L.T("Sistema"),
        };

        [RelayCommand]
        private void CycleLanguage()
        {
            var next = Preferences.Get("AppLang", "system") switch
            {
                "system" => "es",
                "es" => "en",
                _ => "system",
            };
            Preferences.Set("AppLang", next);
            L.Init();
            LanguageLabel = CurrentLangLabel();
            RebuildShellToSettings();
        }

        private static void RebuildShellToSettings()
        {
            if (App.Current?.MainPage is not AppShell) return;
            App.Current.MainPage = new AppShell();
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await Shell.Current.GoToAsync("//App/SettingsPage"); } catch { }
            });
        }

        // T14-C1: la política vive en el server (misma URL que verá Play Console).
        [RelayCommand]
        private async Task OpenPrivacyPolicy()
        {
            try { await Launcher.OpenAsync($"{_settingsService.ServerUrl.TrimEnd('/')}/privacidad.html"); }
            catch { }
        }

        // T14-C1: borrado de cuenta — doble confirmación (alerta + escribir la palabra) porque es
        // irreversible y destruye datos.
        [RelayCommand]
        private async Task DeleteAccount()
        {
            var page = App.Current?.MainPage;
            if (page == null) return;

            var ok = await page.DisplayAlert(L.T("Borrar cuenta"),
                L.T("Se eliminarán tu cuenta, tu mascota, tu historial y tus fotos de forma permanente. Si eres el último miembro de un grupo, el grupo también se borra. Esta acción no se puede deshacer."),
                L.T("Continuar"), L.T("Cancelar"));
            if (!ok) return;

            var word = L.T("BORRAR");
            var typed = await page.DisplayPromptAsync(L.T("Confirmación final"),
                L.F("Escribe {0} para confirmar.", word));
            if (!string.Equals(typed?.Trim(), word, StringComparison.OrdinalIgnoreCase)) return;

            if (!await _authService.DeleteAccountAsync())
            {
                await page.DisplayAlert(L.T("Borrar cuenta"),
                    L.T("No se pudo borrar la cuenta. Revisa tu conexión e inténtalo de nuevo."), "OK");
                return;
            }

            // Mismo reset que el logout del perfil: volver a la ceremonia de nacimiento.
            _gameDataService.SetUser(null);
            Preferences.Remove("HasHatched");
            var birth = _serviceProvider.GetRequiredService<Views.BirthCeremonyPage>();
            if (App.Current != null) App.Current.MainPage = new NavigationPage(birth);
        }

        partial void OnNotificationsEnabledChanged(bool value)
        {
            Preferences.Set("NotificationsEnabled", value);
            
            if (value)
            {
                _notificationService.ShowNotification(L.T("Notificaciones Activadas"), L.T("Recibirás alertas sobre tu mascota."));
            }
            
            SyncPreferences();
        }

        private async void SyncPreferences()
        {
            try
            {
                var userId = _gameDataService.CurrentUser?.Id ?? Guid.Empty;
                var request = new { Theme = IsDarkTheme ? "Dark" : "Light", NotificationsEnabled };
                var url = $"{_settingsService.ServerUrl.TrimEnd('/')}/api/users/{userId}/preferences";
                await _httpClient.PutAsJsonAsync(url, request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing preferences: {ex.Message}");
            }
        }
    }
}
