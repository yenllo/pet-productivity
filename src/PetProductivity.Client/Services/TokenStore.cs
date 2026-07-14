using System.Text.Json;

namespace PetProductivity.Client.Services;

// T14-C0 (A1): los tokens viven en SecureStorage (Keystore de Android), no en Preferences
// (XML en claro). En memoria se cachean porque el AuthHeaderHandler los lee en CADA request.
// Si SecureStorage falla (Keystore corrupto en algunos equipos), cae a Preferences con log:
// peor que Keystore, pero mejor que una app rota. ponytail: sin cifrado propio en el fallback.
public static class TokenStore
{
    private const string AccessKey = "SecureAuthToken";
    private const string RefreshKey = "SecureRefreshToken";

    private static bool _loaded;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static string AccessToken { get; private set; } = string.Empty;
    public static string RefreshToken { get; private set; } = string.Empty;

    // Carga única al primer uso. Migra la sesión legada de Preferences["AuthToken"] (T13)
    // a SecureStorage para no desloguear a nadie al actualizar la app.
    public static async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        await Gate.WaitAsync();
        try
        {
            if (_loaded) return;

            AccessToken = await GetAsync(AccessKey);
            RefreshToken = await GetAsync(RefreshKey);

            var legacy = Preferences.Get("AuthToken", string.Empty);
            if (string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(legacy))
            {
                AccessToken = legacy;
                await SetRawAsync(AccessKey, legacy);
            }
            if (!string.IsNullOrEmpty(legacy)) Preferences.Remove("AuthToken");

            _loaded = true;
        }
        finally { Gate.Release(); }
    }

    public static async Task SetAsync(string accessToken, string refreshToken)
    {
        AccessToken = accessToken ?? string.Empty;
        // Un refresh vacío no pisa el vigente (p. ej. respuesta de un server viejo sin el campo).
        if (!string.IsNullOrEmpty(refreshToken)) RefreshToken = refreshToken;
        await SetRawAsync(AccessKey, AccessToken);
        await SetRawAsync(RefreshKey, RefreshToken);
    }

    public static async Task ClearAsync()
    {
        AccessToken = string.Empty;
        RefreshToken = string.Empty;
        try { SecureStorage.Default.Remove(AccessKey); SecureStorage.Default.Remove(RefreshKey); } catch { }
        Preferences.Remove(AccessKey);
        Preferences.Remove(RefreshKey);
        Preferences.Remove("AuthToken"); // sesión legada
        await Task.CompletedTask;
    }

    // ¿El access token vence dentro de <margin>? (lee el 'exp' del JWT sin validar firma:
    // es solo para decidir refrescar antes de tiempo; la validación real la hace el server).
    public static bool AccessTokenExpiresWithin(TimeSpan margin)
    {
        if (string.IsNullOrEmpty(AccessToken)) return false;
        try
        {
            var parts = AccessToken.Split('.');
            if (parts.Length < 2) return false;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
            var exp = DateTimeOffset.FromUnixTimeSeconds(doc.RootElement.GetProperty("exp").GetInt64());
            return exp - DateTimeOffset.UtcNow < margin;
        }
        catch { return false; }
    }

    private static async Task<string> GetAsync(string key)
    {
        try { return await SecureStorage.Default.GetAsync(key) ?? string.Empty; }
        catch { return Preferences.Get(key, string.Empty); }
    }

    private static async Task SetRawAsync(string key, string value)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) SecureStorage.Default.Remove(key);
            else await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SecureStorage falló ({key}): {ex.Message} — usando Preferences.");
            if (string.IsNullOrEmpty(value)) Preferences.Remove(key);
            else Preferences.Set(key, value);
        }
    }
}
