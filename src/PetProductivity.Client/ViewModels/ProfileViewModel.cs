using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

using PetProductivity.Client.Services;

namespace PetProductivity.Client.ViewModels
{
    public partial class ProfileViewModel : ObservableObject
    {
        private readonly Services.GameDataService _gameDataService;
        private readonly Services.AuthService _authService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty] private string userName = string.Empty;
        [ObservableProperty] private string handle = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsRegistered))]
        private bool isGuest;
        public bool IsRegistered => !IsGuest;

        [ObservableProperty] private string petName = string.Empty;

        // Resumen real (antes placeholders). Antes mostraba un "Nivel" (TotalXp/1000+1) inventado y
        // desconectado de la etapa real (Huevo/Cría/Adulto/Maestro) que Mascota/Stats ya muestran
        // — mismo bug de la iteración 13, en un tercer lugar que no se había tocado. Ver PetVisuals.
        [ObservableProperty] private string stageLabel = string.Empty;
        [ObservableProperty] private double xpProgress;
        [ObservableProperty] private string xpLabel = string.Empty;
        [ObservableProperty] private int streak;
        [ObservableProperty] private int tasksCount;
        [ObservableProperty] private string focusStatsLabel = "🎯 0 días de foco · 0 min";

        // Atributos (4 dimensiones de la mascota personal)
        [ObservableProperty] private string cuerpoXp = "0 XP";
        [ObservableProperty] private string menteXp = "0 XP";
        [ObservableProperty] private string hogarXp = "0 XP";
        [ObservableProperty] private string bienestarXp = "0 XP";

        public ProfileViewModel(Services.GameDataService gameDataService, Services.AuthService authService, IServiceProvider serviceProvider)
        {
            _gameDataService = gameDataService;
            _authService = authService;
            _serviceProvider = serviceProvider;
        }

        public async Task InitializeAsync()
        {
            await _gameDataService.InitializeAsync();
            var user = _gameDataService.CurrentUser;
            if (user == null) return;

            IsGuest = user.Email?.StartsWith("guest_") ?? false;
            // T27-L2 (#24): a un invitado no le muestres el código interno ("Invitado a1b2c3d4"
            // + "@invitado_a1b2c3d4"): nombre limpio y sin handle hasta que se registre.
            UserName = IsGuest ? L.T("Invitado") : user.Username;
            Handle = IsGuest ? string.Empty
                : "@" + (user.Username ?? "invitado").ToLowerInvariant().Replace(" ", "_");
            Streak = user.CurrentStreak;
            TasksCount = user.TotalTasksCompleted;
            FocusStatsLabel = L.F("🎯 {0} días de foco · {1} min", user.FocusStreak, user.TotalFocusMinutes);

            var pet = user.UserPet;
            if (pet != null)
            {
                PetName = pet.Name;
                StageLabel = Services.PetVisuals.StageName(pet.EvolutionStage);
                XpProgress = Services.PetVisuals.StageProgress(pet.EvolutionStage, pet.TotalXp);
                XpLabel = L.F("{0} XP totales", (int)pet.TotalXp);

                CuerpoXp = $"{pet.GetStatValue("Cuerpo"):0} XP";
                MenteXp = $"{pet.GetStatValue("Mente"):0} XP";
                HogarXp = $"{pet.GetStatValue("Hogar"):0} XP";
                BienestarXp = $"{pet.GetStatValue("Bienestar"):0} XP";
            }
        }

        [RelayCommand]
        private async Task GoToAuth() => await Shell.Current.GoToAsync("RegisterPage");

        [RelayCommand]
        private async Task OpenHistory() => await Shell.Current.GoToAsync("HistoryPage");

        [RelayCommand]
        private void Logout()
        {
            _authService.Logout();
            _gameDataService.SetUser(null); // sin esto, la UI seguía mostrando al usuario anterior tras el logout
            Preferences.Remove("HasHatched");
            var birthCeremonyPage = _serviceProvider.GetRequiredService<Views.BirthCeremonyPage>();
            Application.Current.MainPage = new NavigationPage(birthCeremonyPage);
        }
    }
}
