using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

/// <summary>
/// T2 (fase "racha"): aviso nocturno "tu racha muere hoy", server-side gracias a T8 (el server conoce
/// la hora local de cada usuario). Barre cada 30 min; elegibles: hicieron algo AYER, nada HOY, y su
/// reloj local marca 20:00–22:59 (después entran las quiet hours). Anti-spam vía NotificationPolicy.
/// </summary>
public class StreakReminderHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StreakReminderHostedService> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromMinutes(30);

    public const int WindowStartHour = 20; // ventana 20:00–22:59 local

    public StreakReminderHostedService(IServiceScopeFactory scopeFactory, ILogger<StreakReminderHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Streak Reminder Service running. Tick: {Interval}", _tickInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_tickInterval, stoppingToken);
                var sent = await RunSweepAsync(_scopeFactory, ct: stoppingToken);
                if (sent > 0) _logger.LogInformation("Streak reminder: {N} aviso(s) enviados.", sent);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) { _logger.LogError(ex, "Error en el barrido de avisos de racha."); }
        }
    }

    /// <summary>Elegible = ayer hubo actividad, hoy no, y su hora local está en la ventana nocturna.</summary>
    public static bool IsEligible(User u, DateTime? utcNow = null)
    {
        if (u.CurrentStreak < 1 || u.LastActivityDate == null) return false;
        var today = LocalDay.TodayTokenFor(u, utcNow);
        if (u.LastActivityDate.Value.Date != today.AddDays(-1).Date) return false; // hoy ya hizo algo, o la racha ya murió
        var localHour = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, LocalDay.ZoneFor(u)).Hour;
        return localHour >= WindowStartHour; // el techo (23:00+) lo corta NotificationPolicy
    }

    /// <summary>Un barrido (reutilizable desde un disparador dev). Devuelve cuántos avisos salieron.</summary>
    public static async Task<int> RunSweepAsync(IServiceScopeFactory scopeFactory, DateTime? utcNow = null, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<PushService>();

        var candidates = await db.Users
            .Where(u => u.NotificationsEnabled && u.DeviceToken != null && u.CurrentStreak >= 1 && u.LastActivityDate != null)
            .ToListAsync(ct);

        int sent = 0;
        foreach (var u in candidates)
        {
            if (!IsEligible(u, utcNow) || !NotificationPolicy.ShouldSend(u, "streak", utcNow)) continue;
            NotificationPolicy.MarkSent(u, "streak", utcNow);
            await push.SendToUsersAsync(new[] { u.Id },
                $"🔥 Tu racha de {u.CurrentStreak} día(s) está en juego",
                "Una tarea o un foco antes de medianoche la salva.");
            sent++;
        }
        if (sent > 0) await db.SaveChangesAsync(ct);
        return sent;
    }
}
