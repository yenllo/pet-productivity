using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Shared.Models;

using PetProductivity.Client.Services;

namespace PetProductivity.Client.ViewModels
{
    public partial class RegisterViewModel : ObservableObject
    {
        private readonly Services.AuthService _authService;
        private readonly Services.GameDataService _gameDataService;

        public RegisterViewModel(Services.AuthService authService, Services.GameDataService gameDataService)
        {
            _authService = authService;
            _gameDataService = gameDataService;
        }
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private string confirmPassword = string.Empty;

        [RelayCommand]
        private async Task Register()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert(L.T("Error"), L.T("Por favor completa todos los campos."), "OK");
                return;
            }

            if (Password != ConfirmPassword)
            {
               await Application.Current.MainPage.DisplayAlert(L.T("Error"), L.T("Las contraseñas no coinciden."), "OK");
               return;
            }

            User user = null;
            
            // If the user is currently a Guest, upgrade their account instead of creating a new one
            if (_authService.IsLoggedIn && _authService.CurrentUser.Email.StartsWith("guest_"))
            {
                user = await _authService.UpgradeAsync(Name, Email, Password, Archetype.Neutral);
            }
            else
            {
                // Registro directo (sin pasar por invitado): el usuario nunca vio la ceremonia, así que
                // no pudo nombrar a su mascota. Antes el nombre por defecto era `Name + "'s Pet"` —
                // genitivo sajón HARDCODEADO en inglés, en una app cuya política (T16) es que todo texto
                // visible nace en español y se traduce con L. Reproducido en el emulador: registrarse
                // como "Ana" dejaba la mascota llamada "Ana's Pet" incluso con la app en español.
                // (Que este usuario no pueda ELEGIR el nombre es una decisión de diseño, no un bug:
                //  anotada como candidata en tareas/29-ideas-futuras.md.)
                user = await _authService.RegisterAsync(Name, Email, Password, L.F("Mascota de {0}", Name), Archetype.Neutral);
            }

            if (user != null)
            {
                _gameDataService.SetUser(user);
                await NavigateToDashboard();
            }
            else
            {
               await Application.Current.MainPage.DisplayAlert(L.T("Error"), L.T("No se pudo registrar. El correo podría estar en uso."), "OK");
            }
        }

        [RelayCommand]
        private async Task GoogleLogin()
        {
            var (user, cancelled) = await _authService.LoginWithGoogleAsync();
            if (user != null)
            {
                _gameDataService.SetUser(user);
                await NavigateToDashboard();
            }
            else if (!cancelled)
            {
                await Application.Current.MainPage.DisplayAlert("Google", L.T("No se pudo iniciar sesión con Google."), "OK");
            }
        }

        [RelayCommand]
        private async Task NavigateToLogin()
        {
            await Shell.Current.GoToAsync(".."); // Go back
        }

        // Cancelar = volver a donde se venía (mascota personal o login), sin registrarse.
        [RelayCommand]
        private async Task Cancel()
        {
            await Shell.Current.GoToAsync("..");
        }

        private async Task NavigateToDashboard()
        {
            // Si esta pantalla fue empujada sobre una pestaña (p.ej. desde Perfil), quitarla del
            // stack antes de saltar, para que no quede "encima" al volver tras el login con Google.
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.Navigation.PopAsync();
            await Shell.Current.GoToAsync("//App/MascotaPage");
        }
    }
}
