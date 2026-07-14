using System.Net.Http.Json;
#if ANDROID
using Plugin.Firebase.CloudMessaging;
#endif

namespace PetProductivity.Client.Services;

// Pide permiso de notificaciones, obtiene el token FCM y lo registra en el servidor (autenticado).
public class PushRegistration
{
    private readonly HttpClient _http;
    private readonly SettingsService _settings;

    public PushRegistration(HttpClient http, SettingsService settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task RegisterAsync()
    {
#if ANDROID
        try
        {
            await CrossFirebaseCloudMessaging.Current.CheckIfValidAsync();
            var token = await CrossFirebaseCloudMessaging.Current.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                var url = $"{_settings.ServerUrl.TrimEnd('/')}/api/users/me/device-token";
                await _http.PostAsJsonAsync(url, new { Token = token });
                Console.WriteLine($"FCM token registrado ({token.Length} chars)");
            }
        }
        catch (Exception ex) { Console.WriteLine($"Push register failed: {ex.Message}"); }
#else
        await Task.CompletedTask;
#endif
    }
}
