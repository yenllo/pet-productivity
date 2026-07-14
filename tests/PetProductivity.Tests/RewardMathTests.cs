using PetProductivity.Server.Services;
using Xunit;

namespace PetProductivity.Tests;

/// <summary>
/// T15-A: la tabla de economía como contrato. Los valores esperados van en LITERALES a propósito:
/// cambiar el balance debe romper exactamente estos casos y obligar a actualizar la tabla.
/// Orden de la cadena: base(+ritual) → fuera-de-contexto → plausibilidad → dedupe → rendimientos
/// decrecientes → frenesí(solo XP) → foto. Redondeo a int en cada paso.
/// </summary>
public class RewardMathTests
{
    [Theory]
    // ---- caso base y dificultades extremas ----
    [InlineData(5, 10, 1.0, false, false, 0, false, 1.0, 50, 25)]   // base: d5 plano
    [InlineData(1, 10, 1.0, false, false, 0, false, 1.0, 10, 5)]    // mínimo
    [InlineData(10, 10, 1.0, false, false, 0, false, 1.0, 100, 50)] // máximo plano
    // ---- cada multiplicador aislado ----
    [InlineData(5, 10, 1.2, false, false, 0, false, 1.0, 60, 25)]   // ritual ×1.2 (solo XP)
    [InlineData(5, 5, 1.0, false, false, 0, false, 1.0, 25, 12)]    // plausibilidad 5/10
    [InlineData(5, 10, 1.0, true, false, 0, false, 1.0, 12, 6)]     // fuera de contexto ×0.25
    [InlineData(5, 10, 1.0, false, true, 0, false, 1.0, 5, 2)]      // dedupe ×0.1
    [InlineData(5, 10, 1.0, false, false, 4, false, 1.0, 50, 25)]   // 5ª tarea del día: aún completa
    [InlineData(5, 10, 1.0, false, false, 5, false, 1.0, 25, 12)]   // 6ª tarea: ×0.5
    [InlineData(5, 10, 1.0, false, false, 10, false, 1.0, 12, 6)]   // 11ª tarea: ×0.25
    [InlineData(5, 10, 1.0, false, false, 0, true, 1.0, 75, 25)]    // frenesí ×1.5 SOLO XP (T26)
    [InlineData(5, 10, 1.0, false, false, 0, false, 2.0, 100, 50)]  // foto verificada ×2 (ambos)
    // ---- combinaciones ----
    [InlineData(8, 10, 1.0, false, false, 0, true, 2.0, 240, 80)]   // foco grupal: frenesí ×1.5 + foto (T26)
    [InlineData(6, 10, 1.0, false, true, 10, false, 1.0, 1, 0)]     // dedupe + 11ª tarea: casi nada
    [InlineData(4, 7, 1.0, true, false, 0, false, 1.0, 7, 3)]       // fuera de contexto + plausibilidad 7
    [InlineData(9, 10, 1.2, false, false, 0, false, 1.0, 108, 45)]  // ritual + dificultad alta
    [InlineData(10, 10, 1.2, false, false, 0, true, 2.0, 360, 100)] // techo: todo a favor (frenesí ×1.5, T26)
    public void Compute_TablaDeBalance(int difficulty, int plausibility, double ritual,
        bool outOfContext, bool duplicate, int tasksToday, bool frenzy, double photoMult,
        int expectedXp, int expectedGold)
    {
        var (xp, gold) = RewardMath.Compute(difficulty, plausibility, ritual,
            outOfContext, duplicate, tasksToday, frenzy, photoMult);

        Assert.Equal(expectedXp, xp);
        Assert.Equal(expectedGold, gold);
    }
}
