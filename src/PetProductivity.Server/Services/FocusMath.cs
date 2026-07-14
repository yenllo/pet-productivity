using PetProductivity.Shared;

namespace PetProductivity.Server.Services;

/// <summary>
/// Regla de recompensa del modo foco (AC3 v2), pura y testeable. El server mide el tiempo real
/// (no confía en el cliente): si se comprometió una duración hay que haberla servido, y la dificultad
/// queda topada por ella (1 por cada ~15 min).
/// </summary>
public static class FocusMath
{
    public static (bool completed, int difficulty, int servedMinutes) Evaluate(double elapsedMinutes, int targetMinutes)
    {
        var elapsed = Math.Clamp(elapsedMinutes, 0, 240);

        // Tolerancia 0.5 min por desfases de reloj/red entre cuenta atrás del cliente y StartedAt del server.
        if (targetMinutes > 0 && elapsed + 0.5 < targetMinutes)
            return (false, 0, (int)elapsed);

        var served = targetMinutes > 0 ? Math.Min(elapsed, targetMinutes) : elapsed;
        int difficulty = Math.Clamp((int)Math.Ceiling(served / 15.0), 1, 10);
        return (true, difficulty, (int)served);
    }

    /// <summary>
    /// Multiplicador de recompensa por estar verificado por tiempo real (FocusVerifiedMultiplier),
    /// con el bonus opcional de comprobante por foto apilado encima. Una foto ausente o no plausible
    /// deja el multiplicador en el piso verificado — nunca castiga por debajo de él.
    /// </summary>
    public static double VerifiedMultiplier(bool hasProof, bool proofPlausible)
    {
        double mult = Constants.FocusVerifiedMultiplier;
        if (hasProof && proofPlausible) mult *= Constants.PhotoBonusMultiplier;
        return mult;
    }
}
