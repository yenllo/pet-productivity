using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Client.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.ViewModels;

public partial class BirthCeremonyViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private PetSpecies _chosenSpecies;

    [ObservableProperty]
    private string revealedSpeciesImage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartJourneyCommand))]
    private string petName = string.Empty;

    /// <summary>
    /// StartJourney can only execute when the user has typed a non-empty name.
    /// </summary>
    private bool CanStartJourney() => !string.IsNullOrWhiteSpace(PetName) && !IsBusy;

    public BirthCeremonyViewModel(AuthService authService)
    {
        _authService = authService;
        
        // Randomly pick one of the 3 species locally
        int speciesIndex = Random.Shared.Next(0, 3);
        _chosenSpecies = (PetSpecies)speciesIndex;
        // Recién nacida = cría (etapa Baby) de la especie elegida.
        RevealedSpeciesImage = PetVisuals.SpriteFor(_chosenSpecies, EvolutionStage.Baby);
    }

    // Deshabilita Start mientras registra (el primer request puede tardar ~1 min si Render está frío).
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartJourneyCommand))]
    private bool isBusy;

    [RelayCommand(CanExecute = nameof(CanStartJourney))]
    private async Task StartJourney()
    {
        IsBusy = true;
        try
        {
            // Generar cuenta de invitado
            var guestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var guestEmail = $"guest_{guestId}@petproductivity.local";
            var guestPassword = Guid.NewGuid().ToString();
            var guestName = "Invitado " + guestId;

            // Registrar pasando la especie inicial para que el servidor respete la que vio el usuario
            var newUser = await _authService.RegisterAsync(guestName, guestEmail, guestPassword, PetName, Archetype.Neutral, _chosenSpecies);

            if (newUser != null)
            {
                Preferences.Set("HasHatched", true);
                // Replace the root navigation to jump straight to Dashboard, bypassing the ceremony completely.
                Application.Current.MainPage = new AppShell();
                await Shell.Current.GoToAsync("//App/MascotaPage");
            }
            else
            {
                // Sin esto el botón parecía muerto ante un fallo de red / cold start del server.
                await CommunityToolkit.Maui.Alerts.Toast.Make(
                    L.T("No se pudo conectar. Espera unos segundos y vuelve a intentar."),
                    CommunityToolkit.Maui.Core.ToastDuration.Long).Show();
            }
        }
        finally { IsBusy = false; }
    }

    // OJO: aquí NO se marca HasHatched. Antes sí, y era un camino sin retorno: tocar "¿Ya tienes
    // cuenta?" y NO llegar a iniciar sesión (te arrepientes, te equivocas de contraseña, se cae la red,
    // cierras la app) dejaba HasHatched=true igual. Al reabrir, App.xaml.cs ya no muestra la ceremonia
    // → el usuario aterrizaba en el Dashboard con una mascota invitada llamada "Huevo" y especie
    // aleatoria que él nunca eligió, sin forma de volver a nacer. Reproducido en el emulador.
    // Ahora la ceremonia solo se da por superada cuando existe cuenta de verdad: StartJourney (arriba,
    // tras registrar al invitado) o el login/registro exitoso (ProfileViewModel/LoginViewModel navegan
    // al Shell por su cuenta). Si el usuario vuelve atrás sin autenticarse, la ceremonia sigue ahí.
    [RelayCommand]
    private async Task NavigateToLogin()
    {
        Application.Current.MainPage = new AppShell();
        await Shell.Current.GoToAsync("//LoginPage");
    }
}
