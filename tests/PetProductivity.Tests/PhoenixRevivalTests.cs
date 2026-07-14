using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>T3: Fénix — vía acumulativa (grietas por días distintos) y escudo de ausencia.</summary>
public class PhoenixRevivalTests
{
    private static readonly DateTime Day1 = new(2026, 7, 1);
    private static readonly DateTime Day2 = new(2026, 7, 2);
    private static readonly DateTime Day3 = new(2026, 7, 3);

    private static Pet Cristalizada()
    {
        var p = new Pet { Hunger = 0 };
        p.ApplyDamage(999);
        Assert.Equal(PetStatus.Crystallized, p.Status);
        return p;
    }

    [Fact]
    public void TresDiasDistintos_Reviven_ConVidaReducidaYGracia()
    {
        var p = Cristalizada();
        Assert.False(p.AddRevivalCredit(Day1));
        Assert.False(p.AddRevivalCredit(Day2));
        Assert.True(p.AddRevivalCredit(Day3)); // 3ª grieta → revive

        Assert.Equal(PetStatus.Alive, p.Status);
        Assert.Equal(p.MaxHealth * Pet.ReviveHealthFraction, p.Health); // igual que la vía épica
        Assert.NotNull(p.GracePeriodExpiry);
        Assert.Equal(0, p.RevivalProgress);        // criterio 4: el ciclo parte limpio
        Assert.Null(p.LastRevivalCreditDay);
    }

    [Fact]
    public void MismoDia_SoloCuentaUnaGrieta()
    {
        var p = Cristalizada();
        p.AddRevivalCredit(Day1);
        p.AddRevivalCredit(Day1);
        p.AddRevivalCredit(Day1);
        Assert.Equal(1, p.RevivalProgress);
        Assert.Equal(PetStatus.Crystallized, p.Status);
    }

    [Fact]
    public void HazanaEpica_SigueReviviendoAlInstante_YResetaGrietas()
    {
        var p = Cristalizada();
        p.AddRevivalCredit(Day1);
        Assert.True(p.TryRevive(9)); // criterio 3: la vía rápida no cambió
        Assert.Equal(PetStatus.Alive, p.Status);
        Assert.Equal(0, p.RevivalProgress);
    }

    [Fact]
    public void Viva_NoAcumulaGrietas()
    {
        var p = new Pet();
        Assert.False(p.AddRevivalCredit(Day1));
        Assert.Equal(0, p.RevivalProgress);
    }

    // ---- T3-E: escudo de ausencia (DecayMath) ----

    private static readonly DateTime Now = DateTime.SpecifyKind(new DateTime(2026, 7, 10, 12, 0, 0), DateTimeKind.Utc);

    [Fact]
    public void Ausente10Dias_SoloDecaen3Dias_YLoDormidoSePerdona()
    {
        // Última actividad hace 10 días; el reloj de decadencia también parte ahí.
        var lastActivity = Now.AddDays(-10);
        var p = new Pet { Hunger = 100, LastDecayAt = lastActivity };

        int ticks = DecayMath.ApplyPendingDecay(p, Now, lastActivity);

        Assert.Equal(36, ticks);                       // 3 días × 12 ticks/día — no 120
        Assert.Equal(PetStatus.Alive, p.Status);       // criterio 1: viva (y hambrienta: 100-36×5 → 0)
        Assert.Equal(0, p.Hunger);
        Assert.Equal(Now, p.LastDecayAt);              // la deuda dormida no se cobra después
    }

    [Fact]
    public void DuenoActivo_ReglaNormal()
    {
        var p = new Pet { Hunger = 100, LastDecayAt = Now.AddHours(-4) };
        int ticks = DecayMath.ApplyPendingDecay(p, Now, Now.AddDays(-1)); // activo ayer
        Assert.Equal(2, ticks);
        Assert.Equal(90, p.Hunger);
    }

    [Fact]
    public void SinRegistroDeActividad_ReglaNormal()
    {
        var p = new Pet { Hunger = 100, LastDecayAt = Now.AddHours(-4) };
        Assert.Equal(2, DecayMath.ApplyPendingDecay(p, Now, null));
    }
}
