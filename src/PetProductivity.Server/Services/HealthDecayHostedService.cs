using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

public class HealthDecayHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthDecayHostedService> _logger;
    // T10: los números viven en DecayMath (compartidos con la decadencia lazy). Este servicio quedó
    // como barrido BEST-EFFORT: el estado sería correcto igual sin él (lazy); su valor son los push de T2.
    private readonly TimeSpan _tickInterval = TimeSpan.FromMinutes(30);

    public HealthDecayHostedService(IServiceScopeFactory scopeFactory, ILogger<HealthDecayHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health Decay Service running. Tick interval: {Interval}", _tickInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_tickInterval, stoppingToken);
                await ProcessDecayTickAsync(stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Se detiene el servidor
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Health Decay Tick.");
            }
        }
    }

    // T2: umbral de aviso de hambre (se notifica al CRUZARLO, no en cada tick — histéresis).
    // T5: es el MISMO umbral que pone cara hambrienta a la mascota (push y diorama cuentan una historia).
    private const int HungerWarnAt = Pet.HungryAt;

    private async Task ProcessDecayTickAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<PushService>();
        var petLock = scope.ServiceProvider.GetRequiredService<PetWriteLock>();

        // Mascotas personales con dueño y no cristalizadas (las de grupo se procesan aparte abajo, T24#4).
        var users = await db.Users.Include(u => u.UserPet)
            .Where(u => u.UserPet != null && u.UserPet.Status != PetStatus.Crystallized)
            .ToListAsync(stoppingToken);

        int aplicados = 0;
        var avisos = new List<(Guid UserId, string Title, string Body)>();
        foreach (var user in users)
        {
            var pet = user.UserPet!;

            // T10: misma regla y mismo reloj (LastDecayAt) que el lazy → no se pisan por diseño.
            // Lock + reload + save POR mascota: no pisar una recompensa concurrente del usuario.
            using var _ = await petLock.AcquireAsync(pet.Id);
            await db.Entry(pet).ReloadAsync();
            var hungerBefore = pet.Hunger;
            if (DecayMath.ApplyPendingDecay(pet, DateTime.UtcNow, user.LastActivityDate) > 0) // T3-E
            {
                aplicados++;

                // T2-A: el castigo deja de ocurrir a espaldas del usuario. Voz de la mascota.
                if (pet.Status == PetStatus.Crystallized && NotificationPolicy.ShouldSend(user, "crystal"))
                {
                    NotificationPolicy.MarkSent(user, "crystal");
                    avisos.Add((user.Id, $"💎 {pet.Name} se ha cristalizado", "Solo una gran hazaña (dificultad 9+) puede romper el cristal."));
                }
                else if (hungerBefore > 0 && pet.Hunger == 0 && NotificationPolicy.ShouldSend(user, "weak"))
                {
                    NotificationPolicy.MarkSent(user, "weak");
                    avisos.Add((user.Id, $"{pet.Name} se está debilitando 💔", "Lleva demasiado sin comer y su salud empieza a caer."));
                }
                else if (hungerBefore >= HungerWarnAt && pet.Hunger < HungerWarnAt && NotificationPolicy.ShouldSend(user, "hunger"))
                {
                    NotificationPolicy.MarkSent(user, "hunger");
                    avisos.Add((user.Id, $"{pet.Name} tiene hambre 🥺", "Completa una tarea y le repones la pancita."));
                }
            }
            await db.SaveChangesAsync(stoppingToken); // dentro del lock; no-op si nada cambió
        }

        // T24#4: mascotas de GRUPO — decadencia COLECTIVA. Solo pasan hambre si TODO el grupo lleva
        // inactivo (DecayMath.ApplyGroupDecay con la actividad MÁS reciente de sus miembros). Un grupo
        // que trabaja mantiene sana a la compartida; una familia que se apaga del todo la deja caer.
        var groups = await db.Groups.Include(g => g.SharedPet)
            .Where(g => g.SharedPet != null && g.SharedPet.IsHatched && g.SharedPet.Status != PetStatus.Crystallized)
            .ToListAsync(stoppingToken);
        foreach (var group in groups)
        {
            var members = await db.Users
                .Where(u => db.GroupMemberships.Any(m => m.GroupId == group.Id && m.UserId == u.Id))
                .ToListAsync(stoppingToken);
            if (members.Count < 2) continue; // dormida hasta ser funcional (≥2 miembros)

            var pet = group.SharedPet!;
            using var _ = await petLock.AcquireAsync(pet.Id);
            await db.Entry(pet).ReloadAsync();
            var groupLastActivity = members.Max(u => u.LastActivityDate);
            if (DecayMath.ApplyGroupDecay(pet, DateTime.UtcNow, groupLastActivity) > 0)
            {
                aplicados++;
                // Cristalización de la compartida: avisar a TODA la familia (cada uno con su política).
                if (pet.Status == PetStatus.Crystallized)
                    foreach (var m in members.Where(m => NotificationPolicy.ShouldSend(m, "crystal")))
                    {
                        NotificationPolicy.MarkSent(m, "crystal");
                        avisos.Add((m.Id, $"💎 {pet.Name} se ha cristalizado",
                            "La familia estuvo inactiva demasiado tiempo. Una gran hazaña (dificultad 9+) puede revivirla."));
                    }
            }
            await db.SaveChangesAsync(stoppingToken);
        }

        if (aplicados > 0)
            _logger.LogInformation("Decay sweep: {N} mascotas decaídas, {A} avisos.", aplicados, avisos.Count);
        foreach (var a in avisos)
            await push.SendToUsersAsync(new[] { a.UserId }, a.Title, a.Body);
    }
}
