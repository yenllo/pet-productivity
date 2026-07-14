using PetProductivity.Server.Services;
using Xunit;

namespace PetProductivity.Tests;

public class FocusMathTests
{
    [Fact]
    public void AntesDelTarget_NoSeCompleta_SinRecompensa()
    {
        // Comprometió 60 min pero solo sirvió 30 → no completado, dificultad 0.
        var (completed, difficulty, served) = FocusMath.Evaluate(elapsedMinutes: 30, targetMinutes: 60);
        Assert.False(completed);
        Assert.Equal(0, difficulty);
        Assert.Equal(30, served);
    }

    [Fact]
    public void AlCumplirTarget_SeCompleta_DificultadPorTiempoServido()
    {
        // 60 min comprometidos y servidos → ceil(60/15) = 4.
        var (completed, difficulty, served) = FocusMath.Evaluate(elapsedMinutes: 60, targetMinutes: 60);
        Assert.True(completed);
        Assert.Equal(4, difficulty);
        Assert.Equal(60, served);
    }

    [Fact]
    public void DificultadTopadaPorElTarget_AunqueElElapsedSeaMayor()
    {
        // Dejó la sesión 4 h pero comprometió 30 min → dificultad topada en ceil(30/15)=2 (no 10).
        var (completed, difficulty, served) = FocusMath.Evaluate(elapsedMinutes: 240, targetMinutes: 30);
        Assert.True(completed);
        Assert.Equal(2, difficulty);
        Assert.Equal(30, served);
    }

    [Fact]
    public void ToleranciaDeMedioMinuto_CuentaComoCumplido()
    {
        // 59.7 min de 60 comprometidos: dentro de la tolerancia (0.5) → completa.
        var (completed, _, _) = FocusMath.Evaluate(elapsedMinutes: 59.7, targetMinutes: 60);
        Assert.True(completed);
    }

    [Fact]
    public void SinTarget_Legacy_DificultadPorElapsed()
    {
        // target 0 (sesiones viejas): comportamiento previo, dificultad por elapsed.
        var (completed, difficulty, _) = FocusMath.Evaluate(elapsedMinutes: 45, targetMinutes: 0);
        Assert.True(completed);
        Assert.Equal(3, difficulty); // ceil(45/15)
    }

    [Fact]
    public void DosHoras_DanDificultad8()
    {
        var (_, difficulty, _) = FocusMath.Evaluate(elapsedMinutes: 120, targetMinutes: 120);
        Assert.Equal(8, difficulty); // ceil(120/15)
    }
}
