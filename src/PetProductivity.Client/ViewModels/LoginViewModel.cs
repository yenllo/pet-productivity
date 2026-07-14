using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using PetProductivity.Client.Services;

namespace PetProductivity.Client.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly Services.AuthService _authService;
        private readonly Services.GameDataService _gameDataService;

        public LoginViewModel(Services.AuthService authService, Services.GameDataService gameDataService)
        {
            _authService = authService;
            _gameDataService = gameDataService;
        }
        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [RelayCommand]
        private async Task Login()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
            {
                await Application.Current.MainPage.DisplayAlert(L.T("Error"), L.T("Por favor ingresa correo y contraseña."), "OK");
                return;
            }

            var user = await _authService.LoginAsync(Email, Password);
            if (user != null)
            {
                // Sync GameData with the logged user
                _gameDataService.SetUser(user);
                await NavigateToDashboard();
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert(L.T("Error"), L.T("Credenciales inválidas."), "OK");
            }
        }

        [RelayCommand]
        private async Task GoogleLogin()
        {
            var user = await _authService.LoginWithGoogleAsync();
            if (user != null)
            {
                _gameDataService.SetUser(user);
                await NavigateToDashboard();
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Google", L.T("No se pudo iniciar sesión con Google."), "OK");
            }
        }

        [RelayCommand]
        private async Task NavigateToRegister()
        {
            await Shell.Current.GoToAsync("RegisterPage");
        }

        private async Task NavigateToDashboard()
        {
            try
            {
                // Quitar esta pantalla del stack si fue empujada, antes de saltar a la pestaña Mascota.
                if (Shell.Current.Navigation.NavigationStack.Count > 1)
                    await Shell.Current.Navigation.PopAsync();
                // Navigate to the main TabBar route (Mascota personal es ahora el inicio)
                await Shell.Current.GoToAsync("//App/MascotaPage");
            }
            catch (Exception ex)
            {
                 // Fallback if route fails
                 await Application.Current.MainPage.DisplayAlert("Nav Error", $"Ruta fallida: {ex.Message}. Intentando //App/MascotaPage", "OK");
                 await Shell.Current.GoToAsync("//App/MascotaPage");
            }
        }
    }
}
