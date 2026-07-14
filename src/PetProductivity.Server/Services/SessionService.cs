using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;

namespace PetProductivity.Server.Services;

// T14-C0: ciclo de vida del refresh token — emitir, rotar (un uso), revocar.
// El access token (JWT, 60 min) lo emite TokenService; este servicio da la sesión larga.
public class SessionService
{
    public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(90);

    private readonly AppDbContext _context;
    public SessionService(AppDbContext context) => _context = context;

    // Crea un refresh token nuevo para el usuario y devuelve el valor CRUDO (solo viaja una vez).
    public async Task<string> IssueAsync(Guid userId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Hash = HashOf(raw),
            ExpiresUtc = DateTime.UtcNow.Add(RefreshLifetime),
        });
        await _context.SaveChangesAsync();
        return raw;
    }

    // Rotación: valida el token, lo revoca y emite uno nuevo. Null = inválido/expirado/revocado.
    public async Task<(Guid UserId, string NewRaw)?> RotateAsync(string raw)
    {
        var row = await FindAsync(raw);
        if (row is not { } t || !t.IsActive) return null;

        t.RevokedUtc = DateTime.UtcNow;
        var newRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = t.UserId,
            Hash = HashOf(newRaw),
            ExpiresUtc = DateTime.UtcNow.Add(RefreshLifetime),
        });
        await _context.SaveChangesAsync();
        return (t.UserId, newRaw);
    }

    // Logout: revoca ese token (si existe y es del usuario). Best-effort, idempotente.
    public async Task RevokeAsync(Guid userId, string raw)
    {
        var row = await FindAsync(raw);
        if (row != null && row.UserId == userId && row.RevokedUtc == null)
        {
            row.RevokedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    // Cambio de contraseña / ascenso de cuenta: mata todas las sesiones largas previas.
    // (Loop en vez de ExecuteUpdate: son pocas filas por usuario y así corre igual en los tests InMemory.)
    public async Task RevokeAllAsync(Guid userId)
    {
        var rows = await _context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedUtc == null)
            .ToListAsync();
        foreach (var r in rows) r.RevokedUtc = DateTime.UtcNow;
        if (rows.Count > 0) await _context.SaveChangesAsync();
    }

    private Task<RefreshToken?> FindAsync(string raw)
    {
        var hash = HashOf(raw);
        return _context.RefreshTokens.FirstOrDefaultAsync(r => r.Hash == hash);
    }

    private static string HashOf(string raw) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
}
