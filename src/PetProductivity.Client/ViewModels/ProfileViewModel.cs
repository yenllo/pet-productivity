using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

using PetProductivity.Client.Services;
using PetProductivity.Shared.Models;

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
        [ObservableProperty] private string focusStatsLabel = string.Empty;

        // Atributos (4 dimensiones de la mascota personal)
        [ObservableProperty] private string cuerpoXp = "0 XP";
        [ObservableProperty] private string menteXp = "0 XP";
        [ObservableProperty] private string hogarXp = "0 XP";
        [ObservableProperty] private string bienestarXp = "0 XP";

        // T4-A: generaciones / legado.
        [ObservableProperty] private bool showGeneration;              // insignia solo si ya reencarnó (Gen>1)
        [ObservableProperty] private string generationLabel = string.Empty;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RetireCommand))]
        private bool canRetire;                                        // botón visible solo en Maestro
        [ObservableProperty] private bool hasLegacy;
        [ObservableProperty] private List<string> legacy = new();

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

                CanRetire = pet.EvolutionStage == EvolutionStage.Master;
                ShowGeneration = pet.Generation > 1;
                GenerationLabel = L.F("Generación {0}", pet.Generation);
            }

            Legacy = user.RetiredPets
                .OrderByDescending(a => a.Generation)
                .Select(a => L.F("🏆 Gen {0}: {1} — Maestro {2} · {3} XP",
                    a.Generation, a.Name, a.Species, (int)a.FinalTotalXp))
                .ToList();
            HasLegacy = Legacy.Count > 0;
        }

        // T4-A: retirar al Maestro. El usuario decide (no se le quita nada): el Maestro pasa al legado
        // y nace una cría Gen+1. Conserva oro/objetos. Nombre por prompt (no hay pantalla de rename).
        [RelayCommand(CanExecute = nameof(CanRetire))]
        private async Task Retire()
        {
            var pet = _gameDataService.CurrentUser?.UserPet;
            if (pet == null) return;

            bool go = await Shell.Current.DisplayAlert(
                L.F("¿Retirar a {0}?", pet.Name),
                L.T("Se volverá parte de tu legado y nacerá una nueva cría desde el huevo. Conservas tu oro y tus objetos. No se puede deshacer."),
                L.T("Retirar"), L.T("Cancelar"));
            if (!go) return;

            var name = await Shell.Current.DisplayPromptAsync(
                L.T("¿Cómo se llamará la nueva cría?"),
                null, L.T("Comenzar"), L.T("Cancelar"),
                L.T("Nombre de la mascota"), maxLength: 24);
            if (name == null) return; // canceló el prompt

            var (ok, error) = await _gameDataService.RetireAsync(name.Trim());
            if (!ok)
            {
                await Shell.Current.DisplayAlert(L.T("Error"),
                    error ?? L.T("Error de conexión. Inténtalo de nuevo."), "OK");
                return;
            }

            var retiredName = pet.Name;
            await InitializeAsync();
            await Shell.Current.DisplayAlert(L.T("¡Nueva generación!"),
                L.F("🎉 {0} descansa en tu legado. ¡Bienvenida la nueva cría!", retiredName), "OK");
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
