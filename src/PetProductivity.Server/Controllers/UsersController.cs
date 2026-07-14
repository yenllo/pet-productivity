using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using PetProductivity.Server.Data;
using PetProductivity.Shared.Models;
using PetProductivity.Server.Services;

namespace PetProductivity.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PetService _petService;
    private readonly TokenService _tokens;
    private readonly SessionService _sessions; // T14-C0
    private readonly PetWriteLock _petLock;

    public UsersController(AppDbContext context, PetService petService, TokenService tokens, SessionService sessions, PetWriteLock petLock)
    {
        _context = context;
        _petService = petService;
        _tokens = tokens;
        _sessions = sessions;
        _petLock = petLock;
    }

    // El usuario actual según el token (no hace falta saber el id de antemano: ideal tras login con Google).
    [HttpGet("me")]
    public Task<ActionResult<User>> GetMe() => GetUser(User.GetUserId());

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(Guid id)
    {
        if (User.GetUserId() != id) return Forbid(); // un usuario solo se obtiene a sí mismo

        var user = await _context.Users
            .Include(u => u.UserPet)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound();

        // T10: decadencia lazy al materializar la mascota (bajo lock + reload, como las compras).
        // Este endpoint corre en CADA arranque de la app y tras cada acción. El camino lento cuesta 2-3
        // round-trips a Supabase (reload + save) y además serializa la lectura detrás del PetWriteLock —
        // que puede estar tomado por un POST esperando a Gemini. Si no hay nada que aplicar (caso común),
        // nos lo saltamos entero y el GET es 1 sola consulta.
        if (user.UserPet != null && DecayMath.IsDecayPending(user.UserPet, DateTime.UtcNow, user.LastActivityDate))
        {
            using var _ = await _petLock.AcquireAsync(user.UserPet.Id);
            await _context.Entry(user.UserPet).ReloadAsync(); // relee bajo el lock: si no, ticks duplicados
            DecayMath.ApplyPendingDecay(user.UserPet, DateTime.UtcNow, user.LastActivityDate); // T3-E
            await _context.SaveChangesAsync();
        }

        user.Password = string.Empty;
        return user;
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] AuthRequest request)
    {
        var user = await _context.Users
            .Include(u => u.UserPet)
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            // Verificación dummy: iguala el tiempo de respuesta con el caso "usuario existe" para no
            // revelar por timing qué emails están registrados (mismo costo PBKDF2).
            new PasswordHasher<User>().HashPassword(new User(), request.Password);
            return Unauthorized("Email o contraseña incorrectos.");
        }

        var passwordHasher = new PasswordHasher<User>();
        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.Password, request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            // T11-D4: el fallback de contraseñas legadas en texto plano se eliminó tras verificar
            // en BD (2026-07-02) que las 68 cuentas ya están hasheadas (prefijo AQAAAA).
            return Unauthorized("Email o contraseña incorrectos.");
        }
        else if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.Password = passwordHasher.HashPassword(user, request.Password);
        }

        // T8: refrescar la zona horaria si el dispositivo reporta otra (viajes, cambio de equipo).
        var tz = SanitizeTz(request.TimeZoneId);
        if (tz.Length > 0 && user.TimeZoneId != tz) user.TimeZoneId = tz;
        await _context.SaveChangesAsync();

        user.Password = string.Empty;
        return new AuthResponse { User = user, Token = _tokens.CreateToken(user.Id, user.Username),
            RefreshToken = await _sessions.IssueAsync(user.Id) };
    }

    // T8: cap defensivo del id IANA (evita basura kilométrica en BD; LocalDay ya tolera ids inválidos).
    private static string SanitizeTz(string? tz) =>
        string.IsNullOrWhiteSpace(tz) || tz.Length > 64 ? string.Empty : tz.Trim();

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] AuthRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest("Ese email ya está registrado.");
        // Topes de sanidad: nada de nombres/emails de tamaño arbitrario en la BD.
        if ((request.PetName ?? "").Length > 24) return BadRequest("Nombre de mascota demasiado largo (máx. 24).");
        if ((request.Username ?? "").Length > 64 || (request.Email ?? "").Length > 128)
            return BadRequest("Nombre o email demasiado largo.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            Password = string.Empty, // Will be hashed below
            TimeZoneId = SanitizeTz(request.TimeZoneId),
            UserPet = new Pet
            {
                Name = request.PetName,
                CurrentArchetype = request.InitialArchetype,
                Stats = ArchetypeStats.InitializeStats(request.InitialArchetype),
                Species = request.InitialSpecies ?? (PetSpecies)Random.Shared.Next(0, 3),
                TotalXp = 50,
                GoldCoins = 100
            }
        };

        var passwordHasher = new PasswordHasher<User>();
        user.Password = passwordHasher.HashPassword(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        user.Password = string.Empty;
        return new AuthResponse { User = user, Token = _tokens.CreateToken(user.Id, user.Username),
            RefreshToken = await _sessions.IssueAsync(user.Id) };
    }

    [HttpPut("{id}/upgrade")]
    public async Task<ActionResult<AuthResponse>> UpgradeAccount(Guid id, [FromBody] AuthRequest request)
    {
        if (User.GetUserId() != id) return Forbid();

        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound();

        // Check if new email is already taken by someone else
        if (await _context.Users.AnyAsync(u => u.Email == request.Email && u.Id != id))
            return BadRequest("Ese email ya está registrado.");

        user.Username = request.Username;
        user.Email = request.Email;
        var upTz = SanitizeTz(request.TimeZoneId);
        if (upTz.Length > 0) user.TimeZoneId = upTz;
        // (T24) La rama que re-aplicaba arquetipo si TotalXp==0 era inalcanzable: toda mascota nace
        // con 50 XP, y la personal es Neutral por diseño — eliminada.

        var passwordHasher = new PasswordHasher<User>();
        user.Password = passwordHasher.HashPassword(user, request.Password);

        await _context.SaveChangesAsync();

        // T14-C0: cambiar credenciales invalida todas las sesiones largas previas.
        await _sessions.RevokeAllAsync(user.Id);

        user.Password = string.Empty;
        return new AuthResponse { User = user, Token = _tokens.CreateToken(user.Id, user.Username),
            RefreshToken = await _sessions.IssueAsync(user.Id) };
    }

    // T14-C1: borrado de cuenta (requisito de Google Play). Irreversible: elimina usuario,
    // mascota, tareas, focos, fotos y sesiones; sale de los grupos manteniéndolos consistentes.
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe([FromServices] AccountService account) =>
        await account.DeleteAsync(User.GetUserId()) ? NoContent() : NotFound();

    [HttpPost("{id}/ritual/{index}")]
    public async Task<ActionResult<string>> ToggleRitual(Guid id, int index)
    {
        if (User.GetUserId() != id) return Forbid();
        var result = await _petService.ToggleRitualCell(id, index);
        if (result == "User not found") return NotFound();
        return Ok(result);
    }

    // T7: etiquetas de las 9 celdas del ritual (el usuario nombra sus hábitos una vez).
    [HttpPost("me/ritual-labels")]
    public async Task<IActionResult> SetRitualLabels([FromBody] RitualLabelsRequest request)
    {
        var user = await _context.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound();
        if (request.Labels is not { Count: 9 }) return BadRequest("Se esperan 9 etiquetas.");

        user.RitualLabels = string.Join('|', request.Labels
            .Select(l => (l ?? string.Empty).Replace("|", " ").Trim())
            .Select(t => t.Length > 16 ? t[..16] : t));
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Registra/actualiza el token FCM del dispositivo del usuario actual (para push con app cerrada).
    [HttpPost("me/device-token")]
    public async Task<IActionResult> SetDeviceToken([FromBody] DeviceTokenRequest request)
    {
        var user = await _context.Users.FindAsync(User.GetUserId());
        if (user == null) return NotFound();
        user.DeviceToken = request.Token;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id}/preferences")]
    public async Task<IActionResult> UpdatePreferences(Guid id, [FromBody] UpdatePreferencesRequest request)
    {
        if (User.GetUserId() != id) return Forbid();
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.ThemePreference = request.Theme;
        user.NotificationsEnabled = request.NotificationsEnabled;

        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class UpdatePreferencesRequest
{
    public string Theme { get; set; } = "System";
    public bool NotificationsEnabled { get; set; }
}

public class DeviceTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

public class RitualLabelsRequest
{
    public List<string> Labels { get; set; } = new();
}

