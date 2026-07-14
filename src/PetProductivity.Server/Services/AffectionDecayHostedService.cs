using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using PetProductivity.Server.Data;
using PetProductivity.Server.Hubs;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

/// <summary>
/// Decae el afecto de las mascotas compartidas con el tiempo (anti-polizón):
/// quien deja de contribuir ve bajar su afecto y la mascota se pone huraña con él.
/// </summary>
public class AffectionDecayHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AffectionDecayHostedService> _logger;
    private readonly TimeSpan _tickInterval = TimeSpan.FromHours(12);

    public AffectionDecayHostedService(IServiceScopeFactory scopeFactory, ILogger<AffectionDecayHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Affection Decay Service running. Tick interval: {Interval}", _tickInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_tickInterval, stoppingToken);
                await RunDecayAsync(_scopeFactory, stoppingToken);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el tick de decaimiento de afecto.");
            }
        }
    }

    /// <summary>Un tick de decaimiento. Reutilizado por el disparador dev.</summary>
    public static async Task<int> RunDecayAsync(IServiceScopeFactory scopeFactory, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<FamilyHub>>();

        var pets = await db.SharedPets.Where(p => p.Status != PetStatus.Crystallized).ToListAsync(ct);
        foreach (var pet in pets) pet.DecayAllAffection();
        if (pets.Count == 0) return 0;

        await db.SaveChangesAsync(ct);

        // Estado compartido en vivo: difundir el afecto/ánimo decaído por familia.
        var petIds = pets.Select(p => p.Id).ToList();
        var groups = await db.Groups.Where(g => petIds.Contains(g.SharedPetId))
            .Select(g => new { g.Id, g.SharedPetId }).ToListAsync(ct);
        var byPet = pets.ToDictionary(p => p.Id);
        foreach (var g in groups)
            if (byPet.TryGetValue(g.SharedPetId, out var sp))
                await hub.Clients.Group(FamilyHub.Room(g.Id)).SendAsync("PetUpdate", g.Id, PetStateDto.From(sp), ct);

        return pets.Count;
    }
}
