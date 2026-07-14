using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PetProductivity.Server.Controllers;

// OAuth con Google mediado por el servidor: la app abre /start (WebAuthenticator),
// Google redirige a /callback, aquí se canjea el code y se redirige a la app por su esquema propio.
[AllowAnonymous]
[ApiController]
[Route("api/auth/google")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly TokenService _tokens;

    public AuthController(AppDbContext context, IConfiguration config, IHttpClientFactory http, TokenService tokens)
    {
        _context = context;
        _config = config;
        _http = http;
        _tokens = tokens;
    }

    [HttpGet("start")]
    public IActionResult Start([FromQuery] string? link)
    {
        var clientId = _config["Google:ClientId"];
        var redirect = _config["Google:RedirectUri"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirect))
            return Content("Google OAuth no está configurado en el servidor.");

        // El 'link' (id del invitado actual) viaja en state para fusionar su mascota al iniciar sesión.
        var state = Uri.EscapeDataString(link ?? "");
        var url = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirect)}"
            + "&response_type=code&scope=openid%20email%20profile"
            + $"&state={state}&access_type=online&prompt=select_account";
        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        var appCb = _config["Google:AppCallback"] ?? "petproductivity://auth";
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
            return Redirect($"{appCb}?error=denied");

        var clientId = _config["Google:ClientId"];
        var secret = _config["Google:ClientSecret"];
        var redirect = _config["Google:RedirectUri"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(redirect))
            return Redirect($"{appCb}?error=config");

        var http = _http.CreateClient();

        // 1) Canjear el código por tokens.
        var tokenResp = await http.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
            ["redirect_uri"] = redirect,
            ["grant_type"] = "authorization_code"
        }));
        if (!tokenResp.IsSuccessStatusCode)
            return Redirect($"{appCb}?error=token");
        var token = await tokenResp.Content.ReadFromJsonAsync<GoogleToken>();
        if (token?.AccessToken == null)
            return Redirect($"{appCb}?error=token");

        // 2) Perfil del usuario.
        var infoReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
        infoReq.Headers.Authorization = new("Bearer", token.AccessToken);
        var infoResp = await http.SendAsync(infoReq);
        if (!infoResp.IsSuccessStatusCode)
            return Redirect($"{appCb}?error=userinfo");
        var info = await infoResp.Content.ReadFromJsonAsync<GoogleUserInfo>();
        if (info == null || string.IsNullOrEmpty(info.Email))
            return Redirect($"{appCb}?error=userinfo");

        // 3) Buscar / fusionar invitado / crear.
        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Email == info.Email);
        if (user == null)
        {
            // Si veníamos de un invitado, ascender esa cuenta (conserva la mascota).
            if (Guid.TryParse(state, out var linkId))
                user = await _context.Users.Include(u => u.UserPet)
                    .FirstOrDefaultAsync(u => u.Id == linkId && u.Email.StartsWith("guest_"));

            if (user != null)
            {
                user.Email = info.Email;
                user.Username = info.Name ?? info.Email;
            }
            else
            {
                user = new User
                {
                    Username = info.Name ?? info.Email,
                    Email = info.Email,
                    Password = string.Empty,
                    UserPet = new Pet
                    {
                        Name = info.GivenName ?? "Mascota",
                        CurrentArchetype = Archetype.Neutral,
                        Stats = ArchetypeStats.InitializeStats(Archetype.Neutral),
                        Species = (PetSpecies)Random.Shared.Next(0, 3),
                        TotalXp = 50,
                        GoldCoins = 100
                    }
                };
                _context.Users.Add(user);
            }
            await _context.SaveChangesAsync();
        }

        // T14-C0/M5: por el esquema propio (interceptable) ya no viaja el JWT — viaja un código
        // de un solo uso (2 min) que la app canjea por HTTPS en /api/auth/google/exchange.
        var oneTimeCode = GoogleCodes.Create(user.Id);
        return Redirect($"{appCb}?code={Uri.EscapeDataString(oneTimeCode)}");
    }

    private class GoogleToken
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    }

    private class GoogleUserInfo
    {
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("given_name")] public string? GivenName { get; set; }
    }
}
