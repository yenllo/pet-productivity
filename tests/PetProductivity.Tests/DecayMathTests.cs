using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>T10: decadencia lazy — ticks acumulados, cruce hambre→daño, gracia, cristal, resto.</summary>
public class DecayMathTests
{
    private static readonly DateTime Now = DateTime.SpecifyKind(new DateTime(2026, 7, 3, 12, 0, 0), DateTimeKind.Utc);
    private static Pet Viva(double hunger = 100, DateTime? lastDecay = null) =>
        new() { Name = "P", Hunger = hunger, LastDecayAt = lastDecay };

    [Fact]
    public void PrimerContacto_InicializaSinDecaer()
    {
        var p = Viva();
        Assert.Equal(0, DecayMath.ApplyPendingDecay(p, Now));
        Assert.Equal(Now, p.LastDecayAt);
        Assert.Equal(100, p.Hunger);
    }

    [Fact]
    public void MenosDeDosHoras_NoPasaNada()
    {
        var p = Viva(100, Now.AddMinutes(-90));
        Assert.Equal(0, DecayMath.ApplyPendingDecay(p, Now));
        Assert.Equal(100, p.Hunger);
    }

    [Fact]
    public void SeisTicks_DocesHorasDormido_SeRecuperanExactos()
    {
        var p = Viva(100, Now.AddHours(-12));
        Assert.Equal(6, DecayMath.ApplyPendingDecay(p, Now));
        Assert.Equal(70, p.Hunger);            // 6 × -5
        Assert.Equal(Now, p.LastDecayAt);      // reloj al día
    }

    [Fact]
    public void ElRestoDeHoras_SeConserva()
    {
        var p = Viva(100, Now.AddHours(-5));   // 2 ticks + 1 h de resto
        Assert.Equal(2, DecayMath.ApplyPendingDecay(p, Now));
        Assert.Equal(Now.AddHours(-1), p.LastDecayAt);
    }

    [Fact]
    public void HambreEnCero_EmpiezaElDanio()
    {
        var p = Viva(5, Now.AddHours(-8));     // 4 ticks: -5 (queda 0) y 3 de daño ×3
        var hpAntes = p.Health;
        DecayMath.ApplyPendingDecay(p, Now);
        Assert.Equal(0, p.Hunger);
        Assert.Equal(hpAntes - 3 * DecayMath.StarvingDamagePerTick, p.Health);
    }

    [Fact]
    public void Cristalizada_NoDecae_YElRelojNoAcumulaDeuda()
    {
        var p = Viva(0, Now.AddDays(-10));
        for (int i = 0; i < 100; i++) p.ApplyDamage(50); // hasta cristalizar
        Assert.Equal(PetStatus.Crystallized, p.Status);
        var hunger = p.Hunger;
        Assert.Equal(0, DecayMath.ApplyPendingDecay(p, Now));
        Assert.Equal(hunger, p.Hunger);
        Assert.Equal(Now, p.LastDecayAt);
    }

    [Fact]
    public void EscudoDeGracia_BloqueaElDanio_PeroNoElHambre()
    {
        var p = Viva(0, Now.AddHours(-4));
        for (int i = 0; i < 100; i++) p.ApplyDamage(50);
        Assert.True(p.TryRevive(9));           // revive con 20% HP + escudo 24 h
        p.LastDecayAt = Now.AddHours(-4);
        var hp = p.Health;
        DecayMath.ApplyPendingDecay(p, Now);   // 2 ticks muerta de hambre, pero con gracia
        Assert.Equal(hp, p.Health);            // el escudo bloquea el daño
    }

    [Fact]
    public void DecaerHastaCristal_SeDetieneAhi()
    {
        var p = Viva(0, Now.AddDays(-30));     // un mes dormido: cristaliza y PARA
        DecayMath.ApplyPendingDecay(p, Now);
        Assert.Equal(PetStatus.Crystallized, p.Status);
        Assert.Equal(0, p.Health);
    }

    // T24#4: decadencia colectiva de la mascota de grupo.
    [Fact]
    public void Grupo_ConAlguienActivo_NoDecaeNiRetroactivamente()
    {
        var p = Viva(100, Now.AddDays(-10));   // reloj viejo, pero...
        var reciente = Now.AddDays(-1);        // ...alguien del grupo estuvo activo ayer
        Assert.Equal(0, DecayMath.ApplyGroupDecay(p, Now, reciente));
        Assert.Equal(100, p.Hunger);           // protegida incluso de la deuda vieja
        Assert.Equal(Now, p.LastDecayAt);
    }

    [Fact]
    public void Grupo_TodosInactivos_DecaeDesdeQueEmpezoLaInactividad()
    {
        var p = Viva(100);                     // LastDecayAt null
        var ultima = Now.AddDays(-6);          // todo el grupo lleva 6 días sin nada (idle desde el día 3)
        // Decae solo los ~3 días de inactividad real (día 3 → hoy) = 36 ticks de 2 h.
        Assert.Equal(36, DecayMath.ApplyGroupDecay(p, Now, ultima));
        Assert.Equal(0, p.Hunger);             // se quedó sin hambre en el camino
    }

    [Fact]
    public void Grupo_SinActividadRegistrada_Espera()
    {
        var p = Viva(100);
        Assert.Equal(0, DecayMath.ApplyGroupDecay(p, Now, null)); // nadie hizo nada aún: sin castigo
        Assert.Equal(100, p.Hunger);
    }
}
