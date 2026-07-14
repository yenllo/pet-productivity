using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.Services;

// Adjunta el JWT de sesión (Authorization: Bearer) a toda llamada HTTP y mantiene la sesión
// viva (T14-C0): si el access token está por vencer, lo refresca ANTES de enviar; si aun así
// llega un 401, refresca y reintenta una vez (solo requests sin body: un body con stream no
// se puede reenviar — el refresh preventivo cubre el resto). Single-flight con semáforo.
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly SettingsService _settings;
    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public AuthHeaderHandler(SettingsService settings) => _settings = settings;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        await TokenStore.EnsureLoadedAsync();

        // No interceptar las llamadas del propio ciclo de sesión.
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        bool isAuthCall = path.Contains("/api/auth/") || path.EndsWith("/login") || path.EndsWith("/register");

        if (!isAuthCall && !string.IsNullOrEmpty(TokenStore.RefreshToken) &&
            TokenStore.AccessTokenExpiresWithin(TimeSpan.FromMinutes(2)))
            await TryRefreshAsync(ct);

        Attach(request);
        var response = await base.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isAuthCall &&
            request.Content == null && !string.IsNullOrEmpty(TokenStore.RefreshToken))
        {
            if (await TryRefreshAsync(ct))
            {
                response.Dispose();
                Attach(request);
                response = await base.SendAsync(request, ct);
            }
        }
        return response;
    }

    private void Attach(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(TokenStore.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        await RefreshGate.WaitAsync(ct);
        try
        {
            // Otro request pudo refrescar mientras esperábamos el semáforo.
            if (!string.IsNullOrEmpty(TokenStore.AccessToken) &&
                !TokenStore.AccessTokenExpiresWithin(TimeSpan.FromMinutes(2)))
                return true;

            var url = $"{_settings.ServerUrl.TrimEnd('/')}/api/auth/refresh";
            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new { RefreshToken = TokenStore.RefreshToken })
            };
            var resp = await base.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return false; // refresh inválido: el caller verá el 401 normal

            var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
            if (auth == null || string.IsNullOrEmpty(auth.Token)) return false;
            await TokenStore.SetAsync(auth.Token, auth.RefreshToken);
            return true;
        }
        catch { return false; }
        finally { RefreshGate.Release(); }
    }
}
