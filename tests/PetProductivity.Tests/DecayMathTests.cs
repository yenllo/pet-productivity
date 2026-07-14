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

    // --- IsDecayPending: el GET del usuario se salta lock + reload + SaveChanges cuando esto da false.
    // El invariante que lo hace seguro: si dice false, ApplyPendingDecay NO puede mutar NADA. Si se
    // rompiera, el servidor se saltaría una escritura real y la decadencia se perdería en silencio.

    [Theory]
    [InlineData(-1, 0)]      // reloj hace 1 h: menos de un tick
    [InlineData(-119, 0)]    // 1 h 59 min: justo por debajo del tick
    [InlineData(-119, -60)]  // ídem, con dueño activo hace 60 días... (ver nota: activo = hace <3 días)
    public void SiNoHayNadaPendiente_ApplyPendingDecay_NoMutaNada(int minutosDesdeDecay, int diasDesdeActividad)
    {
        var lastActivity = Now.AddDays(diasDesdeActividad);
        var p = Viva(100, Now.AddMinutes(minutosDesdeDecay));
        // Solo comprobamos el invariante cuando el predicado dice "no hay nada que hacer".
        if (!DecayMath.IsDecayPending(p, Now, lastActivity))
        {
            var (hunger, health, status, reloj) = (p.Hunger, p.Health, p.Status, p.LastDecayAt);
            Assert.Equal(0, DecayMath.ApplyPendingDecay(p, Now, lastActivity));
            Assert.Equal(hunger, p.Hunger);
            Assert.Equal(health, p.Health);
            Assert.Equal(status, p.Status);
            Assert.Equal(reloj, p.LastDecayAt);   // ni siquiera el reloj se toca → SaveChanges sería no-op
        }
    }

    [Fact]
    public void HayTickPendiente_ElPredicadoLoDetecta()
    {
        var p = Viva(100, Now.AddHours(-2));                                  // exactamente 1 tick
        Assert.True(DecayMath.IsDecayPending(p, Now, Now.AddHours(-1)));
        Assert.Equal(1, DecayMath.ApplyPendingDecay(p, Now, Now.AddHours(-1)));
    }

    [Fact]
    public void RelojSinInicializar_Cristalizada_YDuenioAusente_TomanElCaminoLento()
    {
        Assert.True(DecayMath.IsDecayPending(Viva(100, null), Now, Now));      // hay que inicializar el reloj

        var cristal = Viva(0, Now.AddMinutes(-10));
        for (int i = 0; i < 100; i++) cristal.ApplyDamage(50);
        Assert.True(DecayMath.IsDecayPending(cristal, Now, Now));              // el reloj salta a ahora (escribe)

        var ausente = Viva(100, Now.AddMinutes(-10));                          // sin ticks, PERO...
        Assert.True(DecayMath.IsDecayPending(ausente, Now, Now.AddDays(-5)));  // ...ausente >3 días: se perdona lo dormido (escribe)
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
