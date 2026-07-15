using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

// T4-A: retirar al Maestro (generaciones/prestigio). Cubre los criterios 3/4/5 del plan T4.
public class PetGenerationTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options);

    private static AccountService Svc(AppDbContext db) =>
        new(db, new GroupService(db, new PresenceService(), new PetWriteLock()));

    private static async Task<User> NewUser(AppDbContext db, double totalXp)
    {
        var u = new User { Username = "renzo", Email = "r@test.local", Password = "x",
            UserPet = new Pet { Name = "Moko", TotalXp = totalXp, GoldCoins = 500,
                Species = PetSpecies.Aqua } };
        db.Users.Add(u);
        await db.SaveChangesAsync();
        return u;
    }

    [Fact] // Criterio 4: no se puede retirar antes de Maestro (2500 XP).
    public async Task No_retira_antes_de_Maestro()
    {
        using var db = Db();
        var user = await NewUser(db, totalXp: 2000); // Adulto, no Maestro

        var (outcome, _) = await Svc(db).RetireAsync(user.Id, "Nueva");

        Assert.Equal(AccountService.RetireOutcome.NotMaster, outcome);
        var pet = (await db.Users.Include(u => u.UserPet).FirstAsync()).UserPet!;
        Assert.Equal("Moko", pet.Name);          // intacta
        Assert.Equal(1, pet.Generation);
        Assert.Empty((await db.Users.FirstAsync()).RetiredPets);
    }

    [Fact] // Criterios 3 y 5: el Maestro pasa al legado; nace cría Gen+1 con XP fresco; oro preservado.
    public async Task Retira_Maestro_crea_ancestro_y_cria_fresca()
    {
        using var db = Db();
        var user = await NewUser(db, totalXp: 2600); // Maestro

        var (outcome, returned) = await Svc(db).RetireAsync(user.Id, "  Nueva Cría  ");
        Assert.Equal(AccountService.RetireOutcome.Ok, outcome);

        var fresh = await db.Users.Include(u => u.UserPet).FirstAsync();
        var pet = fresh.UserPet!;

        // Criterio 3: ancestro consultable con sus stats finales.
        var ancestor = Assert.Single(fresh.RetiredPets);
        Assert.Equal("Moko", ancestor.Name);
        Assert.Equal(2600, ancestor.FinalTotalXp);
        Assert.Equal(1, ancestor.Generation);
        Assert.Equal(PetSpecies.Aqua, ancestor.Species);

        // Criterio 5: cría fresca, mismos umbrales (no hereda XP que salte etapas).
        Assert.Equal("Nueva Cría", pet.Name);        // trim aplicado
        Assert.Equal(2, pet.Generation);
        Assert.Equal(50, pet.TotalXp);
        Assert.NotEqual(EvolutionStage.Master, pet.EvolutionStage);
        Assert.Equal(500, pet.GoldCoins);            // oro NO se pierde al retirar
        Assert.NotNull(returned);                    // devuelve el usuario re-hidratado
    }

    [Fact] // Nombre vacío → cría con nombre por defecto (no queda en blanco).
    public async Task Nombre_vacio_usa_default()
    {
        using var db = Db();
        var user = await NewUser(db, totalXp: 2600);

        await Svc(db).RetireAsync(user.Id, "   ");

        var pet = (await db.Users.Include(u => u.UserPet).FirstAsync()).UserPet!;
        Assert.Equal("Cría", pet.Name);
    }

    [Fact]
    public async Task Usuario_inexistente_devuelve_NotFound()
    {
        using var db = Db();
        var (outcome, _) = await Svc(db).RetireAsync(Guid.NewGuid(), "X");
        Assert.Equal(AccountService.RetireOutcome.NotFound, outcome);
    }
}
