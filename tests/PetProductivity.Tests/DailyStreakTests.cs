using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>T1: la racha diaria real — mismo día, día siguiente, hueco y congelador.</summary>
public class DailyStreakTests
{
    private static readonly DateTime Hoy = DateTime.SpecifyKind(new DateTime(2026, 7, 3), DateTimeKind.Utc);
    private static User NuevoUsuario() => new() { Email = "x@x", Username = "x" };

    [Fact]
    public void PrimeraActividad_RachaUno()
    {
        var u = NuevoUsuario();
        DailyStreak.Advance(u, Hoy);
        Assert.Equal(1, u.CurrentStreak);
        Assert.Equal(Hoy, u.LastActivityDate);
    }

    [Fact]
    public void MismoDia_NoSuma()
    {
        var u = NuevoUsuario();
        DailyStreak.Advance(u, Hoy);
        DailyStreak.Advance(u, Hoy);
        Assert.Equal(1, u.CurrentStreak);
    }

    [Fact]
    public void DiaSiguiente_Suma()
    {
        var u = NuevoUsuario();
        DailyStreak.Advance(u, Hoy.AddDays(-1));
        DailyStreak.Advance(u, Hoy);
        Assert.Equal(2, u.CurrentStreak);
        Assert.Equal(2, u.MaxStreak);
    }

    [Fact]
    public void HuecoSinProteccion_VuelveAUno_YMaxSeConserva()
    {
        var u = NuevoUsuario();
        DailyStreak.Advance(u, Hoy.AddDays(-5));
        DailyStreak.Advance(u, Hoy.AddDays(-4)); // racha 2
        DailyStreak.Advance(u, Hoy);             // hueco de 3 días
        Assert.Equal(1, u.CurrentStreak);
        Assert.Equal(2, u.MaxStreak);
    }

    [Fact]
    public void Congelador_CubreUnDia_YSeConsume()
    {
        var u = NuevoUsuario();
        u.Inventory = new() { [DailyStreak.FreezerItem] = 2 };
        DailyStreak.Advance(u, Hoy.AddDays(-2)); // racha 1... último día activo: anteayer
        var froze = DailyStreak.Advance(u, Hoy); // ayer vacío → congelador
        Assert.True(froze);
        Assert.Equal(2, u.CurrentStreak);
        Assert.Equal(1, u.Inventory[DailyStreak.FreezerItem]);
    }

    [Fact]
    public void Congelador_NoSeGasta_EnHuecosLargos()
    {
        var u = NuevoUsuario();
        u.Inventory = new() { [DailyStreak.FreezerItem] = 1 };
        DailyStreak.Advance(u, Hoy.AddDays(-4));
        var froze = DailyStreak.Advance(u, Hoy); // hueco de 3 días: no se congela ni se gasta
        Assert.False(froze);
        Assert.Equal(1, u.CurrentStreak);
        Assert.Equal(1, u.Inventory[DailyStreak.FreezerItem]);
    }

    [Fact]
    public void SinCongelador_ElHuecoDeUnDia_RompeLaRacha()
    {
        var u = NuevoUsuario();
        DailyStreak.Advance(u, Hoy.AddDays(-2));
        DailyStreak.Advance(u, Hoy);
        Assert.Equal(1, u.CurrentStreak);
    }
}
