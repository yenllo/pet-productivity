namespace PetProductivity.Server.Services;

/// <summary>
/// La economía del juego en un solo lugar (T15-A + T19-A): qué paga una tarea, con cada número
/// nombrado. Función pura (sin BD ni IA) para testearse por tabla — el ORDEN de aplicación y el
/// redondeo a int en cada paso SON parte del contrato de balance (no "arreglar" sin tabla nueva).
/// </summary>
public static class RewardMath
{
    public const int XpPerDifficulty = 10;
    public const int GoldPerDifficulty = 5;
    public const double RitualMultiplier = 1.2;     // tres-en-raya del día completado
    public const int RitualResetDifficulty = 7;     // una tarea ≥7 consume el bonus del ritual
    public const int PlausibilityScale = 10;        // plausibilidad 1-10 divide la recompensa
    public const double OutOfContextFactor = 0.25;  // registrada fuera del contexto del grupo
    public const double DuplicateFactor = 0.1;      // misma descripción (normalizada) en 24 h
    public const int DailyDimAfter = 5;             // desde la 6ª tarea del día rinde DimFactor
    public const int DailyDimHardAfter = 10;        // desde la 11ª rinde HardDimFactor
    public const double DimFactor = 0.5;
    public const double HardDimFactor = 0.25;
    public const double FrenzyXpMultiplier = 1.5;   // T26: solo XP; bajado de ×2 a ×1.5; el oro no se duplica en Frenesí

    // Lo que la tarea repone en la mascota (se aplica en ApplyRewardAsync):
    public const int HungerPerDifficulty = 5;
    public const int HealPerDifficulty = 2;

    public static (int Xp, int Gold) Compute(int difficulty, int plausibility, double ritualMultiplier,
        bool outOfContext, bool duplicate, int tasksToday, bool frenzy, double rewardMultiplier)
    {
        int xp = (int)(difficulty * XpPerDifficulty * ritualMultiplier);
        int gold = difficulty * GoldPerDifficulty;

        if (outOfContext)
        {
            xp = (int)(xp * OutOfContextFactor);
            gold = (int)(gold * OutOfContextFactor);
        }

        // Plausibilidad (anti-mentira): división entera, igual que siempre.
        xp = xp * plausibility / PlausibilityScale;
        gold = gold * plausibility / PlausibilityScale;

        if (duplicate)
        {
            xp = (int)(xp * DuplicateFactor);
            gold = (int)(gold * DuplicateFactor);
        }

        double dim = tasksToday < DailyDimAfter ? 1.0
                   : tasksToday < DailyDimHardAfter ? DimFactor : HardDimFactor;
        xp = (int)(xp * dim);
        gold = (int)(gold * dim);

        if (frenzy) xp = (int)(xp * FrenzyXpMultiplier);

        // Comprobante por foto del foco: multiplica ambos (mult ≥ 1, nunca castiga).
        xp = (int)(xp * rewardMultiplier);
        gold = (int)(gold * rewardMultiplier);

        return (xp, gold);
    }
}
