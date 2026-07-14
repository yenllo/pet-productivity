using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Controllers;

// T14-C0: ciclo de sesión — refresh (rotación), logout (revocación) y el canje del
// código de un solo uso del login con Google (M5: el JWT ya no viaja por el esquema
// petproductivity://, interceptable; viaja un código efímero que se canjea por HTTPS).
[ApiController]
[Route("api/auth")]
public class SessionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TokenService _tokens;
    private readonly SessionService _sessions;

    public SessionController(AppDbContext context, TokenService tokens, SessionService sessions)
    {
        _context = context;
        _tokens = tokens;
        _sessions = sessions;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken)) return Unauthorized();

        var rotated = await _sessions.RotateAsync(request.RefreshToken);
        if (rotated is not { } r) return Unauthorized();

        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Id == r.UserId);
        if (user == null) return Unauthorized();

        user.Password = string.Empty;
        return new AuthResponse
        {
            User = user,
            Token = _tokens.CreateToken(user.Id, user.Username),
            RefreshToken = r.NewRaw,
        };
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        if (!string.IsNullOrEmpty(request.RefreshToken))
            await _sessions.RevokeAsync(User.GetUserId(), request.RefreshToken);
        return Ok();
    }

    // ---- Canje del código de un solo uso del OAuth de Google ----

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("google/exchange")]
    public async Task<ActionResult<AuthResponse>> Exchange([FromBody] ExchangeRequest request)
    {
        if (string.IsNullOrEmpty(request.Code) || !GoogleCodes.TryConsume(request.Code, out var userId))
            return Unauthorized();

        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Unauthorized();

        user.Password = string.Empty;
        return new AuthResponse
        {
            User = user,
            Token = _tokens.CreateToken(user.Id, user.Username),
            RefreshToken = await _sessions.IssueAsync(user.Id),
        };
    }

    public class RefreshRequest { public string RefreshToken { get; set; } = string.Empty; }
    public class ExchangeRequest { public string Code { get; set; } = string.Empty; }
}

// Códigos efímeros del callback de Google (un solo uso, 2 min). En memoria: el server es
// una sola instancia (Render free) y el canje ocurre segundos después del callback.
// ponytail: si algún día hay varias instancias, mover a la BD o a un cache compartido.
public static class GoogleCodes
{
    private static readonly ConcurrentDictionary<string, (Guid UserId, DateTime Expires)> Codes = new();

    public static string Create(Guid userId)
    {
        // Limpieza oportunista de códigos vencidos (el diccionario se mantiene diminuto).
        foreach (var kv in Codes)
            if (kv.Value.Expires < DateTime.UtcNow) Codes.TryRemove(kv.Key, out _);

        var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        Codes[code] = (userId, DateTime.UtcNow.AddMinutes(2));
        return code;
    }

    public static bool TryConsume(string code, out Guid userId)
    {
        userId = default;
        if (!Codes.TryRemove(code, out var entry)) return false;
        if (entry.Expires < DateTime.UtcNow) return false;
        userId = entry.UserId;
        return true;
    }
}
