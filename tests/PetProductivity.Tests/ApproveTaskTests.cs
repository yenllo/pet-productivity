using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PetProductivity.Server.Data;
using PetProductivity.Server.Hubs;
using PetProductivity.Server.Services;
using PetProductivity.Shared;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

public class ApproveTaskTests
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
            NoPush(db));

    // PushService sin credenciales = no-op silencioso (mismo comportamiento que en dev).
    private static PushService NoPush(AppDbContext db) =>
        new(db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), NullLogger<PushService>.Instance);

    [Fact]
    public async Task ApproveTask_MayoriaDeLosOtros_AplicaRecompensa_Y_BorraLaAprobacion()
    {
        var db = NewDb();
        var requester = Guid.NewGuid();
        var approver = Guid.NewGuid();
        db.Users.Add(new User { Id = requester, Username = "B", Email = "guest_b@x.local" });
        db.Users.Add(new User { Id = approver, Username = "A", Email = "guest_a@x.local" });

        var pet = new SharedPet
        {
            Name = "Compartida",
            IsHatched = true,
            CurrentArchetype = Archetype.Guild,
            Stats = ArchetypeStats.InitializeStats(Archetype.Guild)
        };
        db.SharedPets.Add(pet);
        var group = new Group { Name = "Equipo", SharedPetId = pet.Id };
        db.Groups.Add(group);
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = requester, Role = GroupRole.Member });
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = approver, Role = GroupRole.Member });
        var ta = new TaskApproval
        {
            GroupId = group.Id,
            PetId = pet.Id,
            RequesterUserId = requester,
            Description = "coordiné la reunión",
            Difficulty = 4,
            Category = "General",
            XpEarned = 40,
            GoldEarned = 20
        };
        db.TaskApprovals.Add(ta);
        await db.SaveChangesAsync();

        var xpBefore = pet.TotalXp;
        var svc = NewPetService(db);

        var (approved, votes, needed) = await svc.ApproveTaskAsync(ta.Id, approver);

        Assert.True(approved);
        Assert.Equal(1, needed);                 // others = 1 → mayoría = 1
        Assert.Equal(1, votes);
        Assert.Empty(db.TaskApprovals);          // aprobada → borrada
        var fresh = await db.SharedPets.FindAsync(pet.Id);
        Assert.True(fresh!.TotalXp > xpBefore);  // recompensa aplicada al pet
    }

    [Fact]
    public async Task ApproveTask_ElSolicitanteNoSeAutoAprueba()
    {
        var db = NewDb();
        var requester = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Users.Add(new User { Id = requester, Username = "B", Email = "b@x" });
        db.Users.Add(new User { Id = other, Username = "A", Email = "a@x" });
        var pet = new SharedPet { Name = "P", IsHatched = true, CurrentArchetype = Archetype.Guild, Stats = ArchetypeStats.InitializeStats(Archetype.Guild) };
        db.SharedPets.Add(pet);
        var group = new Group { Name = "G", SharedPetId = pet.Id };
        db.Groups.Add(group);
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = requester, Role = GroupRole.Member });
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = other, Role = GroupRole.Member });
        var ta = new TaskApproval { GroupId = group.Id, PetId = pet.Id, RequesterUserId = requester, Description = "x", Difficulty = 3, Category = "General", XpEarned = 30, GoldEarned = 15 };
        db.TaskApprovals.Add(ta);
        await db.SaveChangesAsync();

        // El propio solicitante "aprueba": no cuenta, no se premia.
        var (approved, votes, _) = await NewPetService(db).ApproveTaskAsync(ta.Id, requester);
        Assert.False(approved);
        Assert.Equal(0, votes);
        Assert.Single(db.TaskApprovals);
    }

    [Fact]
    public async Task FocoGrupal_ImmediateShared_PremiaAlInstante_SinAprobacion()
    {
        var db = NewDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User { Id = uid, Username = "A", Email = "a@x" });
        var pet = new SharedPet { Name = "P", IsHatched = true, CurrentArchetype = Archetype.Guild, Stats = ArchetypeStats.InitializeStats(Archetype.Guild) };
        db.SharedPets.Add(pet);
        var group = new Group { Name = "G", SharedPetId = pet.Id };
        db.Groups.Add(group);
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = uid, Role = GroupRole.Member });
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = Guid.NewGuid(), Role = GroupRole.Member });
        await db.SaveChangesAsync();
        var before = pet.TotalXp;

        var r = await NewPetService(db).ProcessTaskCompletion(uid, pet.Id, "foco", true, timedDifficulty: 5, immediateShared: true);

        Assert.True(r.XpEarned > 0);
        Assert.Empty(db.TaskApprovals);            // foco verificado por tiempo → sin validación social
        var fresh = await db.SharedPets.FindAsync(pet.Id);
        Assert.True(fresh!.TotalXp > before);       // recompensa aplicada de inmediato
    }

    [Fact]
    public async Task Comprobante_Verificado_DuplicaLaRecompensa()
    {
        var full = await RunFocusReward(1.0);
        var doble = await RunFocusReward(Constants.PhotoBonusMultiplier);
        Assert.True(full > 0);
        Assert.Equal(full * 2, doble);
    }

    private static async Task<int> RunFocusReward(double mult)
    {
        var db = NewDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User { Id = uid, Username = "A", Email = "a@x" });
        var pet = new SharedPet { Name = "P", IsHatched = true, CurrentArchetype = Archetype.Guild, Stats = ArchetypeStats.InitializeStats(Archetype.Guild) };
        db.SharedPets.Add(pet);
        var group = new Group { Name = "G", SharedPetId = pet.Id };
        db.Groups.Add(group);
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = uid, Role = GroupRole.Member });
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = Guid.NewGuid(), Role = GroupRole.Member });
        await db.SaveChangesAsync();
        var r = await NewPetService(db).ProcessTaskCompletion(uid, pet.Id, "foco", true, timedDifficulty: 6, rewardMultiplier: mult, immediateShared: true);
        return r.XpEarned;
    }

    // T6-B: la aprobación vencida se APLICA (no se borra) y es idempotente.
    [Fact]
    public async Task AutoApprove_AplicaUnaVez_YEsIdempotente()
    {
        var db = NewDb();
        var requester = Guid.NewGuid();
        db.Users.Add(new User { Id = requester, Username = "R", Email = "r@x" });
        var pet = new SharedPet { Name = "P", IsHatched = true, CurrentArchetype = Archetype.Guild, Stats = ArchetypeStats.InitializeStats(Archetype.Guild) };
        db.SharedPets.Add(pet);
        var group = new Group { Name = "G", SharedPetId = pet.Id };
        db.Groups.Add(group);
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = requester, Role = GroupRole.Member });
        var ta = new TaskApproval { GroupId = group.Id, PetId = pet.Id, RequesterUserId = requester, Description = "x", Difficulty = 4, Category = "General", XpEarned = 40, GoldEarned = 20 };
        db.TaskApprovals.Add(ta);
        await db.SaveChangesAsync();
        var before = pet.TotalXp;
        var svc = NewPetService(db);

        Assert.True(await svc.AutoApproveExpiredAsync(ta.Id));   // vencida → se aplica
        Assert.False(await svc.AutoApproveExpiredAsync(ta.Id));  // segundo tick → no-op
        Assert.Empty(db.TaskApprovals);
        Assert.Single(db.TaskItems);                             // recompensa exactamente una vez
        var fresh = await db.SharedPets.FindAsync(pet.Id);
        Assert.Equal(before + 40, fresh!.TotalXp);
    }

    // T11-D2: dos votantes distintos a la vez (needed=2) — sin el lock por approvalId un voto se
    // pisaba (last-write-wins, reproducido en vivo en T21) o el premio podía duplicarse.
    [Fact]
    public async Task ApproveTask_VotosConcurrentes_NoSePierdenVotos_NiSePremiaDoble()
    {
        var root = new Microsoft.EntityFrameworkCore.Storage.InMemoryDatabaseRoot();
        var name = $"db-{Guid.NewGuid()}";
        AppDbContext Db() => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name, root).Options);
        var sharedLock = new PetWriteLock(); // singleton real: compartido entre "requests"
        PetService Svc(AppDbContext db) => new(db,
            new AiJudgeService(NullLogger<AiJudgeService>.Instance, new SilentAi()),
            NullLogger<PetService>.Instance, new PresenceService(), new FakeHubContext(), sharedLock, NoPush(db));

        var requester = Guid.NewGuid();
        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();
        Guid petId, taId;
        double xpBefore;
        await using (var db = Db())
        {
            db.Users.AddRange(
                new User { Id = requester, Username = "R", Email = "r@x" },
                new User { Id = v1, Username = "V1", Email = "v1@x" },
                new User { Id = v2, Username = "V2", Email = "v2@x" });
            var pet = new SharedPet { Name = "P", IsHatched = true, CurrentArchetype = Archetype.Guild, Stats = ArchetypeStats.InitializeStats(Archetype.Guild) };
            db.SharedPets.Add(pet);
            var group = new Group { Name = "G", SharedPetId = pet.Id };
            db.Groups.Add(group);
            db.GroupMemberships.AddRange(
                new GroupMembership { GroupId = group.Id, UserId = requester, Role = GroupRole.Member },
                new GroupMembership { GroupId = group.Id, UserId = v1, Role = GroupRole.Member },
                new GroupMembership { GroupId = group.Id, UserId = v2, Role = GroupRole.Member });
            var ta = new TaskApproval { GroupId = group.Id, PetId = pet.Id, RequesterUserId = requester, Description = "x", Difficulty = 4, Category = "General", XpEarned = 40, GoldEarned = 20 };
            db.TaskApprovals.Add(ta);
            await db.SaveChangesAsync();
            petId = pet.Id; taId = ta.Id; xpBefore = pet.TotalXp;
        }

        // Cada votante con su propio contexto (como dos requests HTTP), lock compartido.
        await using var dbA = Db();
        await using var dbB = Db();
        var results = await Task.WhenAll(
            Svc(dbA).ApproveTaskAsync(taId, v1),
            Svc(dbB).ApproveTaskAsync(taId, v2));

        Assert.Contains(results, r => r.approved);   // el 2º voto llegó a needed=2: nada se perdió
        await using var check = Db();
        Assert.Empty(check.TaskApprovals);           // aprobada → borrada (una sola vez)
        Assert.Single(check.TaskItems);              // recompensa aplicada exactamente una vez
        var fresh = await check.SharedPets.FindAsync(petId);
        Assert.Equal(xpBefore + 40, fresh!.TotalXp);
    }

    // Fakes mínimos → TestFakes.cs (compartidos con RitualTests).
}
