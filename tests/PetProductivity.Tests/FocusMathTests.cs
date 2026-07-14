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

    // ---- VerifiedMultiplier: el piso por estar verificado por tiempo real, más el bonus de foto ----
    // Regresión del hallazgo real: un foco de 5 min (dificultad 1) pagaba MENOS que escribir a mano
    // "leer" (dificultad 2 del juez) — 10xp/5oro vs 20xp/10oro. Sin este piso, verificar de verdad
    // salía perdiendo frente a una mentira de una palabra.

    [Fact]
    public void SinComprobante_SoloElPisoVerificado()
    {
        Assert.Equal(PetProductivity.Shared.Constants.FocusVerifiedMultiplier,
            FocusMath.VerifiedMultiplier(hasProof: false, proofPlausible: false));
    }

    [Fact]
    public void ComprobanteFallido_NoBajaDelPisoVerificado()
    {
        // "nunca por debajo": una foto que no convenció no debe pagar menos que no haber tomado foto.
        Assert.Equal(PetProductivity.Shared.Constants.FocusVerifiedMultiplier,
            FocusMath.VerifiedMultiplier(hasProof: true, proofPlausible: false));
    }

    [Fact]
    public void ComprobantePlausible_SeApilaSobreElPiso()
    {
        var esperado = PetProductivity.Shared.Constants.FocusVerifiedMultiplier * PetProductivity.Shared.Constants.PhotoBonusMultiplier;
        Assert.Equal(esperado, FocusMath.VerifiedMultiplier(hasProof: true, proofPlausible: true));
    }

    [Fact]
    public void AIgualDificultad_LoVerificadoPagaMasQueElTextoLibre()
    {
        // El caso real reportado: un foco de 5 min (dificultad mínima 1) pagó MENOS que escribir a mano
        // "leer" (que el juez calificó 2, no 1 — el otro fix de esta ronda le pone coto a eso). A IGUAL
        // dificultad, lo verificado por tiempo real debe pagar más que un texto sin verificar: es lo que
        // hace el multiplicador, independiente de si el juez ya capea bien los textos vagos.
        var (_, difficultyFoco, _) = FocusMath.Evaluate(elapsedMinutes: 5, targetMinutes: 5);
        var multFoco = FocusMath.VerifiedMultiplier(hasProof: false, proofPlausible: false);
        var (xpFoco, _) = RewardMath.Compute(difficultyFoco, plausibility: 10, ritualMultiplier: 1.0,
            outOfContext: false, duplicate: false, tasksToday: 0, frenzy: false, rewardMultiplier: multFoco);

        var (xpManualVago, _) = RewardMath.Compute(difficulty: difficultyFoco, plausibility: 10, ritualMultiplier: 1.0,
            outOfContext: false, duplicate: false, tasksToday: 0, frenzy: false, rewardMultiplier: 1.0);

        Assert.True(xpFoco > xpManualVago, $"foco verificado ({xpFoco}) debería pagar más que el texto libre a igual dificultad ({xpManualVago})");
    }
}
