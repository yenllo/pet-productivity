using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;

namespace PetProductivity.Server.Services;

/// <summary>
/// Limpia restos huérfanos del modo foco / validación social: sesiones de foco que quedaron abiertas
/// (la app se mató a mitad) y aprobaciones de tarea que nadie resolvió. Mantiene la BD ordenada.
/// </summary>
public class FocusCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FocusCleanupHostedService> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromHours(1);

    // T6-B: ventana de veto de la familia; después, el silencio otorga (auto-aprobación).
    public const int ApprovalAutoApproveHours = 48;

    public FocusCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<FocusCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Focus Cleanup Service running. Tick interval: {Interval}", _tickInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_tickInterval, stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (TaskCanceledException) { /* server detenido */ }
            catch (Exception ex) { _logger.LogError(ex, "Error en limpieza de foco."); }
        }
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var focusCutoff = DateTime.UtcNow.AddHours(-6);       // un foco no dura más de 4 h (clamp); 6 h = huérfano
        var approvalCutoff = DateTime.UtcNow.AddHours(-ApprovalAutoApproveHours);
        var proofCutoff = DateTime.UtcNow.AddDays(-30);       // fotos de comprobante: privacidad/almacenamiento, el veredicto vive en TaskItem.ProofVerdict

        var staleFocus = await db.FocusSessions.Where(s => s.StartedAt < focusCutoff).ToListAsync(ct);
        var expiredApprovalIds = await db.TaskApprovals.Where(t => t.CreatedAt < approvalCutoff).Select(t => t.Id).ToListAsync(ct);
        var staleGroupFocus = await db.GroupFocusSessions.Where(s => s.StartedAt < focusCutoff).ToListAsync(ct);
        var staleProofs = await db.FocusProofs.Where(p => p.CreatedAt < proofCutoff).ToListAsync(ct);

        // Refresh tokens muertos: sin esto la tabla crece 1 fila por login para siempre. Los revocados
        // se conservan 7 días antes de purgarse.
        // OJO: la "detección de reuso = cascada de revocación" que este comentario prometía NUNCA se
        // implementó (verificado 2026-07-14: SessionService.RotateAsync trata un token YA REVOCADO
        // igual que uno expirado — 401 y ya, sin revocar el resto de la sesión del usuario). Los 7
        // días de retención por sí solos no hacen nada de seguridad; sin la cascada, son solo
        // ventana para trazas de auditoría manual. Candidata real (no implementada) en
        // tareas/29-ideas-futuras.md.
        var now = DateTime.UtcNow;
        var revokedCutoff = now.AddDays(-7);
        var deadTokens = await db.RefreshTokens
            .Where(r => r.RevokedUtc < revokedCutoff || r.ExpiresUtc < now)
            .ExecuteDeleteAsync(ct);
        if (deadTokens > 0) _logger.LogInformation("Purga de sesiones: {N} refresh tokens muertos borrados.", deadTokens);

        // T6-B: las aprobaciones vencidas se APLICAN, no se borran — el esfuerzo reportado ya no se
        // esfuma en silencio ("el silencio de la familia otorga"; el anti-cheat real ya actuó al calcular).
        if (expiredApprovalIds.Count > 0)
        {
            var pets = scope.ServiceProvider.GetRequiredService<PetService>();
            foreach (var id in expiredApprovalIds)
                await pets.AutoApproveExpiredAsync(id);
        }

        if (staleFocus.Count == 0 && staleGroupFocus.Count == 0 && staleProofs.Count == 0) return;

        db.FocusSessions.RemoveRange(staleFocus);
        db.GroupFocusSessions.RemoveRange(staleGroupFocus);
        db.FocusProofs.RemoveRange(staleProofs);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Focus cleanup: {Focus} sesiones, {Group} grupales y {Proof} fotos borradas; {Appr} aprobaciones auto-aplicadas.",
            staleFocus.Count, staleGroupFocus.Count, staleProofs.Count, expiredApprovalIds.Count);
    }
}
