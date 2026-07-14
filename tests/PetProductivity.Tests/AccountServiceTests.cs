using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

// T14-C1: el borrado de cuenta elimina TODO rastro del usuario y deja los grupos consistentes.
public class AccountServiceTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options);

    private static AccountService Svc(AppDbContext db) =>
        new(db, new GroupService(db, new PresenceService(), new PetWriteLock()));

    private static async Task<User> NewUser(AppDbContext db, string name)
    {
        var u = new User { Username = name, Email = $"{name}@test.local", Password = "x",
            UserPet = new Pet { Name = "Mascota de " + name } };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u;
    }

    [Fact]
    public async Task Delete_borra_usuario_mascota_y_todos_sus_datos()
    {
        using var db = Db();
        var user = await NewUser(db, "borrable");
        db.TaskItems.Add(new TaskItem { UserId = user.Id, Description = "tarea" });
        db.FocusSessions.Add(new FocusSession { UserId = user.Id });
        db.FocusProofs.Add(new FocusProof { UserId = user.Id });
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, Hash = "h1", ExpiresUtc = DateTime.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();

        Assert.True(await Svc(db).DeleteAsync(user.Id));

        Assert.Empty(db.Users);
        Assert.Empty(db.Pets);
        Assert.Empty(db.TaskItems);
        Assert.Empty(db.FocusSessions);
        Assert.Empty(db.FocusProofs);
        Assert.Empty(db.RefreshTokens);
    }

    [Fact]
    public async Task Delete_sale_del_grupo_y_el_grupo_sobrevive_con_los_demas()
    {
        using var db = Db();
        var groups = new GroupService(db, new PresenceService(), new PetWriteLock());
        var svc = new AccountService(db, groups);

        var ana = await NewUser(db, "ana");
        var beto = await NewUser(db, "beto");
        var group = await groups.CreateGroupAsync(ana.Id, "Hogar", Archetype.Neutral, 4);
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = beto.Id });
        await db.SaveChangesAsync();

        Assert.True(await svc.DeleteAsync(ana.Id));

        // El grupo sigue vivo para beto, sin membresía ni rastros de ana.
        Assert.NotNull(await db.Groups.FindAsync(group.Id));
        Assert.Empty(db.GroupMemberships.Where(m => m.UserId == ana.Id));
        Assert.NotEmpty(db.GroupMemberships.Where(m => m.UserId == beto.Id));
        var pet = await db.SharedPets.FirstAsync();
        Assert.False(pet.UserAffection.ContainsKey(ana.Id));
    }

    [Fact]
    public async Task Delete_del_ultimo_miembro_borra_el_grupo_y_su_mascota()
    {
        using var db = Db();
        var groups = new GroupService(db, new PresenceService(), new PetWriteLock());
        var svc = new AccountService(db, groups);

        var solo = await NewUser(db, "solo");
        await groups.CreateGroupAsync(solo.Id, "Fantasma", Archetype.Neutral, 4);

        Assert.True(await svc.DeleteAsync(solo.Id));

        Assert.Empty(db.Groups);
        Assert.Empty(db.SharedPets);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Delete_de_usuario_inexistente_devuelve_false()
    {
        using var db = Db();
        Assert.False(await Svc(db).DeleteAsync(Guid.NewGuid()));
    }
}
