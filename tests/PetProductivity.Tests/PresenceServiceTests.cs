using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

public class PresenceServiceTests
{
    private static readonly Guid G = Guid.NewGuid();
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    [Fact]
    public void Frenesi_ActivoConDosTrabajando_OffPorDebajo()
    {
        var p = new PresenceService();
        p.Join("c1", A, new() { G }, SyncStatus.Working);
        Assert.False(p.IsFrenzyActive(G)); // 1 trabajando

        p.Join("c2", B, new() { G }, SyncStatus.Working);
        Assert.True(p.IsFrenzyActive(G));  // 2 trabajando

        p.SetStatus(A, SyncStatus.Available);
        Assert.False(p.IsFrenzyActive(G)); // baja a 1
    }

    [Fact]
    public void SetStatusWithFrenzy_DetectaLaTransicionOnUnaSolaVez()
    {
        var p = new PresenceService();
        p.Join("c1", A, new() { G }, SyncStatus.Working);
        p.Join("c2", B, new() { G }, SyncStatus.Available);

        // B pasa a Working -> arranca el Frenesí (transición off->on).
        var (affected, started) = p.SetStatusWithFrenzy(B, SyncStatus.Working);
        Assert.Contains(G, affected);
        Assert.Contains(G, started);

        // Un cambio que mantiene el Frenesí no lo reporta como "recién iniciado".
        var (_, started2) = p.SetStatusWithFrenzy(A, SyncStatus.Working);
        Assert.DoesNotContain(G, started2);
    }

    [Fact]
    public void Disconnect_SacaAlUsuario_YApagaElFrenesi()
    {
        var p = new PresenceService();
        p.Join("c1", A, new() { G }, SyncStatus.Working);
        p.Join("c2", B, new() { G }, SyncStatus.Working);
        Assert.True(p.IsFrenzyActive(G));

        var affected = p.Disconnect("c2");
        Assert.Contains(G, affected);
        Assert.False(p.IsFrenzyActive(G));
    }
}
