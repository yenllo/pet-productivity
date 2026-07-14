using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>T7: regresión de ToggleRitualCell — el tablero nombrable no debe cambiar la mecánica.</summary>
public class RitualTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options);

    private static PetService NewPetService(AppDbContext db) =>
        new(db,
            new AiJudgeService(NullLogger<AiJudgeService>.Instance, new SilentAi()),
            NullLogger<PetService>.Instance,
            new PresenceService(),
            new FakeHubContext(),
            new PetWriteLock(),
            new PushService(db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), NullLogger<PushService>.Instance));

    private static async Task<(PetService svc, AppDbContext db, User user)> SeedAsync()
    {
        var db = NewDb();
        var user = new User { Id = Guid.NewGuid(), Username = "R", Email = "guest_r@x.local" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (NewPetService(db), db, user);
    }

    [Fact]
    public async Task Toggle_EnciendeYApagaLaCelda()
    {
        var (svc, _, user) = await SeedAsync();
        Assert.Equal("1,0,0,0,0,0,0,0,0", await svc.ToggleRitualCell(user.Id, 0));
        Assert.Equal("0,0,0,0,0,0,0,0,0", await svc.ToggleRitualCell(user.Id, 0));
    }

    [Fact]
    public async Task Linea_ActivaElMultiplicador_YRomperlaLoApaga()
    {
        var (svc, _, user) = await SeedAsync();
        await svc.ToggleRitualCell(user.Id, 0);
        await svc.ToggleRitualCell(user.Id, 1);
        await svc.ToggleRitualCell(user.Id, 2); // fila completa
        Assert.Equal(RewardMath.RitualMultiplier, user.ActiveXpMultiplier);

        await svc.ToggleRitualCell(user.Id, 1); // rompe la línea
        Assert.Equal(1.0, user.ActiveXpMultiplier);
    }

    [Fact]
    public async Task NuevoDia_ReseteaTableroYMultiplicador()
    {
        var (svc, _, user) = await SeedAsync();
        await svc.ToggleRitualCell(user.Id, 0);
        await svc.ToggleRitualCell(user.Id, 1);
        await svc.ToggleRitualCell(user.Id, 2);

        user.LastRitualReset = user.LastRitualReset.AddDays(-1); // simula que la línea fue "ayer"
        var state = await svc.ToggleRitualCell(user.Id, 4);      // primer toggle del día nuevo

        Assert.Equal("0,0,0,0,1,0,0,0,0", state); // tablero limpio + solo la celda tocada
        Assert.Equal(1.0, user.ActiveXpMultiplier);
    }
}
