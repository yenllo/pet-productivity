using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

public class GroupServiceTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options);

    private static async Task<(GroupService svc, AppDbContext db, Guid creator)> SeedAsync()
    {
        var db = NewDb();
        var creator = Guid.NewGuid();
        db.Users.Add(new User { Id = creator, Username = "Creador", Email = "guest_x@x.local" });
        await db.SaveChangesAsync();
        return (new GroupService(db, new PresenceService(), new PetWriteLock()), db, creator);
    }

    private static async Task AddMemberAsync(AppDbContext db, Guid groupId, Guid userId)
    {
        db.GroupMemberships.Add(new GroupMembership { GroupId = groupId, UserId = userId, Role = GroupRole.Member });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Hatch_RequiereDosMiembros()
    {
        var (svc, _, creator) = await SeedAsync();
        var group = await svc.CreateGroupAsync(creator, "Equipo", Archetype.Scholar, 6);

        // 1 miembro → no se puede hacer nacer todavía.
        await Assert.ThrowsAsync<GroupException>(() => svc.VoteToHatchAsync(group.Id, creator));
        Assert.False(group.SharedPet!.IsHatched);
    }

    [Fact]
    public async Task Hatch_NaceSoloCuandoTodosLosMiembrosVotan()
    {
        var (svc, db, creator) = await SeedAsync();
        var group = await svc.CreateGroupAsync(creator, "Equipo", Archetype.Scholar, 6);
        var member2 = Guid.NewGuid();
        await AddMemberAsync(db, group.Id, member2);

        // Primer voto (1 de 2): aún no nace.
        var r1 = await svc.VoteToHatchAsync(group.Id, creator);
        Assert.False(r1.hatched);
        Assert.Equal(1, r1.votes);
        Assert.Equal(2, r1.members);
        Assert.False(group.SharedPet!.IsHatched);

        // Segundo voto (2 de 2): nace y se limpian los votos.
        var r2 = await svc.VoteToHatchAsync(group.Id, member2);
        Assert.True(r2.hatched);
        Assert.True(group.SharedPet!.IsHatched);
        Assert.Empty(group.SharedPet.HatchVotes);
    }

    [Fact]
    public async Task Leave_LimpiaVotoYAfectoDelQueSeVa()
    {
        var (svc, db, creator) = await SeedAsync();
        var group = await svc.CreateGroupAsync(creator, "Equipo", Archetype.Scholar, 6);
        var member2 = Guid.NewGuid();
        var member3 = Guid.NewGuid();
        await AddMemberAsync(db, group.Id, member2);
        await AddMemberAsync(db, group.Id, member3);

        group.SharedPet!.HatchVotes.Add(member3);
        group.SharedPet.UserAffection[member3] = 70;
        await db.SaveChangesAsync();

        await svc.LeaveGroupAsync(group.Id, member3);

        Assert.DoesNotContain(member3, group.SharedPet.HatchVotes);
        Assert.False(group.SharedPet.UserAffection.ContainsKey(member3));
    }

    // ---- T24: bordes de salida del grupo ----

    [Fact]
    public async Task Leave_QuitaElVotoFantasmaDeTaskApprovals()
    {
        var (svc, db, creator) = await SeedAsync();
        var group = await svc.CreateGroupAsync(creator, "Equipo", Archetype.Scholar, 6);
        var b = Guid.NewGuid(); var c = Guid.NewGuid();
        await AddMemberAsync(db, group.Id, b);
        await AddMemberAsync(db, group.Id, c);

        // Tarea pendiente del creador; C vota y se va: su voto no debe quedar contando.
        var ta = new TaskApproval { GroupId = group.Id, PetId = group.SharedPetId, RequesterUserId = creator, Description = "x", Difficulty = 3, Category = "General", XpEarned = 30, GoldEarned = 15 };
        ta.Approvals.Add(c);
        db.TaskApprovals.Add(ta);
        await db.SaveChangesAsync();

        await svc.LeaveGroupAsync(group.Id, c);

        var fresh = await db.TaskApprovals.SingleAsync();
        Assert.DoesNotContain(c, fresh.Approvals); // sin voto fantasma: B aún debe votar
    }

    [Fact]
    public async Task Leave_DelSolicitante_BorraSusTareasPendientes()
    {
        var (svc, db, creator) = await SeedAsync();
        var group = await svc.CreateGroupAsync(creator, "Equipo", Archetype.Scholar, 6);
        var b = Guid.NewGuid();
        await AddMemberAsync(db, group.Id, b);
        db.TaskApprovals.Add(new TaskApproval { GroupId = group.Id, PetId = group.SharedPetId, RequesterUserId = b, Description = "x", Difficulty = 3, Category = "General", XpEarned = 30, GoldEarned = 15 });
        await db.SaveChangesAsync();

        await svc.LeaveGroupAsync(group.Id, b);

        Assert.Empty(db.TaskApprovals); // sin huérfanas del que se fue
    }

    [Fact]
    public async Task Leave_UnanimidadPasiva_HaceNacerElHuevo()
    {
        var (svc, db, creator) = await SeedAsync();
        var group = await svc.CreateGroupAsync(creator, "Equipo", Archetype.Scholar, 6);
        var b = Guid.NewGuid(); var c = Guid.NewGuid();
        await AddMemberAsync(db, group.Id, b);
        await AddMemberAsync(db, group.Id, c);

        // A y B ya votaron; C (sin votar) se va → los restantes son unánimes → nace.
        await svc.VoteToHatchAsync(group.Id, creator);
        await svc.VoteToHatchAsync(group.Id, b);
        Assert.False(group.SharedPet!.IsHatched);

        await svc.LeaveGroupAsync(group.Id, c);

        Assert.True(group.SharedPet.IsHatched);
    }

    [Fact]
    public async Task Leave_UnanimidadPasiva_CompletaSolicitudDeUnion()
    {
        var (svc, db, creator) = await SeedAsync();
        var group = await svc.CreateGroupAsync(creator, "Equipo", Archetype.Scholar, 6);
        var b = Guid.NewGuid(); var x = Guid.NewGuid();
        await AddMemberAsync(db, group.Id, b);
        db.Users.Add(new User { Id = x, Username = "X", Email = "x@x" });
        await db.SaveChangesAsync();

        // X solicita; solo el creador aprueba (falta B) → pendiente. B se va → unánime → X entra.
        var req = await svc.RequestJoinByCodeAsync(group.InviteCode, x);
        await svc.ApproveJoinAsync(req.Id, creator);
        Assert.Single(db.JoinRequests);

        await svc.LeaveGroupAsync(group.Id, b);

        Assert.Empty(db.JoinRequests);
        Assert.True(await db.GroupMemberships.AnyAsync(m => m.GroupId == group.Id && m.UserId == x));
    }
}
