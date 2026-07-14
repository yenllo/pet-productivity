using System.Net.Http.Json;
using PetProductivity.Shared.Models;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Authentication;

namespace PetProductivity.Client.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;

    public User? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser != null;
    public string CurrentToken => TokenStore.AccessToken; // T14-C0: SecureStorage via TokenStore

    public AuthService(SettingsService settingsService, HttpClient httpClient)
    {
        _settingsService = settingsService;
        _httpClient = httpClient;
    }

    private string Base => _settingsService.ServerUrl.TrimEnd('/');

    // Guarda los tokens primero (los lee el AuthHeaderHandler) y fija el usuario.
    private async Task StoreSessionAsync(AuthResponse auth)
    {
        await TokenStore.SetAsync(auth.Token, auth.RefreshToken);
        CurrentUser = auth.User;
    }

    public async Task<bool> EnsureGuestOrLoggedInAsync()
    {
        if (IsLoggedIn) return true;
        await TokenStore.EnsureLoadedAsync();

        // 1) Sesión por token guardado (Google o cualquier login previo): el handler adjunta el Bearer
        //    y, con T14-C0, refresca solo si está por vencer (refresh token rotatorio en SecureStorage).
        // T13: el token solo se descarta si el server lo RECHAZA (401/403). Un fallo de red o un 5xx
        // conserva la sesión guardada — antes, abrir la app sin señal destruía la sesión (fatal para
        // cuentas Google, que no tienen credenciales guardadas) y al volver la red se creaba un invitado.
        if (!string.IsNullOrEmpty(CurrentToken))
        {
            try
            {
                var resp = await _httpClient.GetAsync($"{Base}/api/users/me");
                if (resp.IsSuccessStatusCode)
                    CurrentUser = await resp.Content.ReadFromJsonAsync<User>();
                else if (resp.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                    await TokenStore.ClearAsync(); // sesión inválida de verdad (el handler ya intentó refrescar)
                else
                    return false; // server enfermo: sesión intacta, se reintenta después
            }
            catch { return false; } // sin red: sesión intacta, se reintenta después
            if (CurrentUser != null)
            {
                await MigrateLegacyCredentialsAsync();
                return true;
            }
        }

        // 2) SOLO migración de sesiones viejas (pre-C0): si quedaron credenciales guardadas de una
        //    versión anterior, un último login las convierte en refresh token y se BORRAN del disco.
        //    Las sesiones nuevas persisten por refresh token; la contraseña ya no se guarda (A1).
        var savedEmail = Preferences.Get("SavedEmail", string.Empty);
        var savedPassword = Preferences.Get("SavedPassword", string.Empty);
        if (!string.IsNullOrEmpty(savedEmail) && !string.IsNullOrEmpty(savedPassword))
        {
            var user = await LoginAsync(savedEmail, savedPassword);
            Preferences.Remove("SavedEmail");
            Preferences.Remove("SavedPassword");
            if (user != null) return true;
        }

        // 3) Sin sesión: crear cuenta de invitado.
        var guestId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var guestEmail = $"guest_{guestId}@petproductivity.local";
        var guestPassword = Guid.NewGuid().ToString();
        var guestName = "Invitado " + guestId;

        var newUser = await RegisterAsync(guestName, guestEmail, guestPassword, "Huevo", Archetype.Neutral); // T16: default ES
        return newUser != null;
    }

    // T14-C0/A1: sesiones legadas (token de 30 días de Preferences, sin refresh token) traen la
    // contraseña guardada en claro. Mientras el token viejo siga vivo, un re-login one-shot la
    // convierte en refresh token y la BORRA del disco. Si el login falla (rate-limit, server caído),
    // las credenciales se conservan para reintentar en la próxima apertura — son la única red de
    // seguridad del invitado legado hasta que tenga refresh token.
    private async Task MigrateLegacyCredentialsAsync()
    {
        var savedEmail = Preferences.Get("SavedEmail", string.Empty);
        var savedPassword = Preferences.Get("SavedPassword", string.Empty);
        if (string.IsNullOrEmpty(savedEmail) && string.IsNullOrEmpty(savedPassword)) return;

        if (!string.IsNullOrEmpty(TokenStore.RefreshToken))
        {
            // Ya hay sesión moderna: las credenciales viejas sobran.
            Preferences.Remove("SavedEmail");
            Preferences.Remove("SavedPassword");
            return;
        }

        if (!string.IsNullOrEmpty(savedEmail) && !string.IsNullOrEmpty(savedPassword) &&
            await LoginAsync(savedEmail, savedPassword) != null)
        {
            Preferences.Remove("SavedEmail");
            Preferences.Remove("SavedPassword");
        }
    }

    public async Task<User?> LoginAsync(string email, string password)
    {
        try
        {
            var request = new AuthRequest { Email = email, Password = password, TimeZoneId = TimeZoneInfo.Local.Id };
            var response = await _httpClient.PostAsJsonAsync($"{Base}/api/users/login", request);
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (auth != null)
                {
                    await StoreSessionAsync(auth); // A1: nada de contraseñas en disco
                    return CurrentUser;
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"Login Error: {ex.Message}"); }
        return null;
    }

    public async Task<User?> RegisterAsync(string username, string email, string password, string petName, Archetype initialArchetype = Archetype.Neutral, PetSpecies? initialSpecies = null)
    {
        try
        {
            var request = new AuthRequest
            {
                Username = username,
                Email = email,
                Password = password,
                PetName = petName,
                InitialArchetype = initialArchetype,
                InitialSpecies = initialSpecies,
                TimeZoneId = TimeZoneInfo.Local.Id
            };
            var response = await _httpClient.PostAsJsonAsync($"{Base}/api/users/register", request);
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (auth != null)
                {
                    await StoreSessionAsync(auth); // A1: nada de contraseñas en disco
                    return CurrentUser;
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"Register Error: {ex.Message}"); }
        return null;
    }

    public async Task<User?> UpgradeAsync(string newUsername, string newEmail, string newPassword, Archetype initialArchetype = Archetype.Neutral)
    {
        if (CurrentUser == null) return null;
        try
        {
            var request = new AuthRequest
            {
                Username = newUsername,
                Email = newEmail,
                Password = newPassword,
                PetName = CurrentUser.UserPet?.Name ?? "Huevo",
                InitialArchetype = initialArchetype,
                TimeZoneId = TimeZoneInfo.Local.Id
            };
            var response = await _httpClient.PutAsJsonAsync($"{Base}/api/users/{CurrentUser.Id}/upgrade", request);
            if (response.IsSuccessStatusCode)
            {
                var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (auth != null)
                {
                    await StoreSessionAsync(auth); // A1: nada de contraseñas en disco
                    return CurrentUser;
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"Upgrade Error: {ex.Message}"); }
        return null;
    }

    // Login con Google vía OAuth mediado por el servidor (WebAuthenticator → /api/auth/google/start).
    // T14-C0/M5: el callback redirige con ?code= (un solo uso, 2 min) y la app lo canjea por HTTPS
    // en /api/auth/google/exchange — el JWT ya no viaja por el esquema propio interceptable.
    // Si hay un invitado activo, su id viaja en 'link' para conservar la mascota al ascender la cuenta.
    public async Task<User?> LoginWithGoogleAsync()
    {
        try
        {
            var link = (CurrentUser != null && CurrentUser.Email.StartsWith("guest_")) ? CurrentUser.Id.ToString() : "";
            var authUrl = new Uri($"{Base}/api/auth/google/start?link={link}");
            var callbackUrl = new Uri("petproductivity://auth");

            var result = await WebAuthenticator.Default.AuthenticateAsync(authUrl, callbackUrl);

            if (result.Properties.TryGetValue("error", out var err) && !string.IsNullOrEmpty(err))
            {
                Console.WriteLine($"Google login error: {err}");
                return null;
            }
            if (result.Properties.TryGetValue("code", out var code) && !string.IsNullOrEmpty(code))
            {
                var resp = await _httpClient.PostAsJsonAsync($"{Base}/api/auth/google/exchange", new { Code = code });
                if (!resp.IsSuccessStatusCode) return null;
                var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
                if (auth == null) return null;
                await StoreSessionAsync(auth);
                Preferences.Remove("SavedEmail");
                Preferences.Remove("SavedPassword");
                return CurrentUser;
            }
        }
        catch (TaskCanceledException) { /* el usuario cerró el navegador */ }
        catch (Exception ex) { Console.WriteLine($"Google login error: {ex.Message}"); }
        return null;
    }

    // T14-C1: borrado de cuenta. El server elimina todo (usuario, mascota, historial, fotos);
    // localmente equivale a un logout. Devuelve false si el server no confirmó (la sesión se conserva).
    public async Task<bool> DeleteAccountAsync()
    {
        try
        {
            var resp = await _httpClient.DeleteAsync($"{Base}/api/users/me");
            if (!resp.IsSuccessStatusCode) return false;
        }
        catch (Exception ex) { Console.WriteLine($"DeleteAccount Error: {ex.Message}"); return false; }

        CurrentUser = null;
        await TokenStore.ClearAsync();
        Preferences.Remove("SavedEmail");
        Preferences.Remove("SavedPassword");
        Preferences.Remove("GoogleUserId");
        return true;
    }

    public void Logout()
    {
        // Revocación best-effort de la sesión larga en el server (no bloquea el logout local).
        var refresh = TokenStore.RefreshToken;
        if (!string.IsNullOrEmpty(refresh))
            _ = _httpClient.PostAsJsonAsync($"{Base}/api/auth/logout", new { RefreshToken = refresh });

        CurrentUser = null;
        _ = TokenStore.ClearAsync();
        Preferences.Remove("SavedEmail");
        Preferences.Remove("SavedPassword");
        Preferences.Remove("GoogleUserId");
    }
}
