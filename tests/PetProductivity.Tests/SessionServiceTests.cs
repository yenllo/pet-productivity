using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Controllers;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using Xunit;

namespace PetProductivity.Tests;

// T14-C0: ciclo del refresh token (emitir → rotar → revocar) y códigos de un solo uso de Google.
public class SessionServiceTests
{
    private static AppDbContext Db() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"db-{Guid.NewGuid()}").Options);

    [Fact]
    public async Task Rotate_devuelve_token_nuevo_y_revoca_el_anterior()
    {
        using var db = Db();
        var svc = new SessionService(db);
        var userId = Guid.NewGuid();

        var raw = await svc.IssueAsync(userId);
        var rotated = await svc.RotateAsync(raw);

        Assert.NotNull(rotated);
        Assert.Equal(userId, rotated!.Value.UserId);
        Assert.NotEqual(raw, rotated.Value.NewRaw);

        // El token viejo quedó revocado: reusarlo (robo/replay) falla.
        Assert.Null(await svc.RotateAsync(raw));
        // El nuevo sí rota.
        Assert.NotNull(await svc.RotateAsync(rotated.Value.NewRaw));
    }

    [Fact]
    public async Task Rotate_rechaza_tokens_desconocidos()
    {
        using var db = Db();
        var svc = new SessionService(db);
        Assert.Null(await svc.RotateAsync("no-existe"));
    }

    [Fact]
    public async Task Revoke_mata_la_sesion_y_solo_la_del_usuario_dueno()
    {
        using var db = Db();
        var svc = new SessionService(db);
        var userId = Guid.NewGuid();
        var raw = await svc.IssueAsync(userId);

        // Otro usuario no puede revocar un token ajeno.
        await svc.RevokeAsync(Guid.NewGuid(), raw);
        Assert.NotNull(await svc.RotateAsync(raw)); // sigue vivo (y ya rotó)

        var raw2 = await svc.IssueAsync(userId);
        await svc.RevokeAsync(userId, raw2);
        Assert.Null(await svc.RotateAsync(raw2)); // revocado de verdad
    }

    [Fact]
    public async Task RevokeAll_invalida_todas_las_sesiones_del_usuario()
    {
        using var db = Db();
        var svc = new SessionService(db);
        var userId = Guid.NewGuid();
        var a = await svc.IssueAsync(userId);
        var b = await svc.IssueAsync(userId);
        var otro = await svc.IssueAsync(Guid.NewGuid());

        await svc.RevokeAllAsync(userId);

        Assert.Null(await svc.RotateAsync(a));
        Assert.Null(await svc.RotateAsync(b));
        Assert.NotNull(await svc.RotateAsync(otro)); // las de otros usuarios no se tocan
    }

    [Fact]
    public void GoogleCodes_es_de_un_solo_uso()
    {
        var userId = Guid.NewGuid();
        var code = GoogleCodes.Create(userId);

        Assert.True(GoogleCodes.TryConsume(code, out var got));
        Assert.Equal(userId, got);
        Assert.False(GoogleCodes.TryConsume(code, out _)); // segundo canje falla
        Assert.False(GoogleCodes.TryConsume("inventado", out _));
    }
}
