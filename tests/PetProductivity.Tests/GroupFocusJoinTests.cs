using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PetProductivity.Server.Controllers;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>
/// Regresión: unirse a un foco grupal reutilizando una FocusSession personal vieja para la misma
/// mascota compartida (alcanzable de verdad navegando "Registrar tarea" desde el detalle de un grupo,
/// que pasa por el foco SOLO en vez de "Foco grupal") debía realinear StartedAt/TargetMinutes al gfs
/// actual. Sin el fix, /complete mediría contra el reloj VIEJO — si ya estaba vencido, recompensa
/// instantánea sin haber esperado nada de la sesión grupal real.
/// </summary>
public class GroupFocusJoinTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options);

    private static FocusController NewController(AppDbContext db, Guid actingUser)
    {
        var pet = new PetService(db,
            new AiJudgeService(NullLogger<AiJudgeService>.Instance, new SilentAi()),
            NullLogger<PetService>.Instance,
            new PresenceService(),
            new FakeHubContext(),
            new PetWriteLock(),
            new PushService(db, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), NullLogger<PushService>.Instance));

        var controller = new FocusController(db, pet, new SilentAi(), new FakeHubContext(), NullLogger<FocusController>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, actingUser.ToString()) }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
        return controller;
    }

    [Fact]
    public async Task GroupStart_ConSesionSolaViejaParaLaMismaMascota_RealineaElReloj()
    {
        var db = NewDb();
        var uid = Guid.NewGuid();
        db.Users.Add(new User { Id = uid, Username = "U", Email = "u@x.local" });

        var sharedPet = new SharedPet { Name = "Compartida", IsHatched = true };
        db.SharedPets.Add(sharedPet);
        var group = new Group { Name = "Equipo", SharedPetId = sharedPet.Id, MaxMembers = 6 };
        db.Groups.Add(group);
        db.GroupMemberships.Add(new GroupMembership { GroupId = group.Id, UserId = uid, Role = GroupRole.Member });

        // El hueco real: una FocusSession SOLA abandonada sobre la mascota COMPARTIDA (petId = sharedPet.Id),
        // con un reloj viejo ya vencido y un target chico — exactamente lo que dejaría TaskPage→"Foco" si
        // se navegó ahí desde el detalle del grupo sin pasar por "Foco grupal".
        var staleStart = DateTime.UtcNow.AddHours(-2);
        db.FocusSessions.Add(new FocusSession { UserId = uid, PetId = sharedPet.Id, StartedAt = staleStart, TargetMinutes = 5 });
        await db.SaveChangesAsync();

        var controller = NewController(db, uid);
        var result = await controller.GroupStart(new GroupFocusStartRequest { GroupId = group.Id, TargetMinutes = 60, Description = "Estudiar" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var info = Assert.IsType<GroupFocusInfo>(ok.Value);

        var fs = await db.FocusSessions.FindAsync(info.FocusSessionId);
        Assert.NotNull(fs);
        // Debe reflejar el foco grupal REAL, no el reloj viejo — si esto fallara, StartedAt seguiría
        // siendo `staleStart` (vencido hace rato) y TargetMinutes sería 5, no 60.
        Assert.Equal(info.StartedAt, fs!.StartedAt);
        Assert.Equal(60, fs.TargetMinutes);
        Assert.NotEqual(staleStart, fs.StartedAt);
    }
}
