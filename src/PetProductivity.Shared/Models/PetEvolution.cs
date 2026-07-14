namespace PetProductivity.Shared.Models;

/// <summary>
/// Evolution stages for the pet
/// </summary>
public enum EvolutionStage
{
    Egg = 0,      // 0-50 total XP
    Baby = 1,     // 51-600 total XP
    Adult = 2,    // 601-2500 total XP
    Master = 3    // 2501+ total XP
}

/// <summary>
/// Visual evolution thresholds and helpers
/// </summary>
public static class PetEvolution
{
    // Umbrales de etapa (T26, meta del dueño: Egg→Master ≈ 2-4 semanas de uso constante).
    // Con ~150 XP/día: Baby el día 1 (dopamina inmediata), Adult ~día 4, Master ~día 16.
    public const double EggTobabyThreshold = 50;
    public const double BabyToAdultThreshold = 600;
    public const double AdultToMasterThreshold = 2500;

    // Stat thresholds for visual changes (dominant stat)
    public const double MinorVisualChangeThreshold = 50;   // Small change (e.g., color tint)
    public const double MajorVisualChangeThreshold = 100;  // Major change (e.g., glasses, muscles)
    public const double MasterVisualChangeThreshold = 200; // Master change (e.g., wings, aura)

    /// <summary>
    /// Calculate evolution stage based on total XP
    /// </summary>
    public static EvolutionStage CalculateEvolutionStage(double totalXp)
    {
        if (totalXp < EggTobabyThreshold) return EvolutionStage.Egg;
        if (totalXp < BabyToAdultThreshold) return EvolutionStage.Baby;
        if (totalXp < AdultToMasterThreshold) return EvolutionStage.Adult;
        return EvolutionStage.Master;
    }

    /// <summary>
    /// Get the dominant stat (highest value)
    /// </summary>
    public static string GetDominantStat(Dictionary<string, double> stats)
    {
        if (stats == null || stats.Count == 0)
            return "None";

        return stats.OrderByDescending(kvp => kvp.Value).First().Key;
    }

    /// <summary>
    /// Get visual evolution level for a specific stat
    /// 0 = No change, 1 = Minor, 2 = Major, 3 = Master
    /// </summary>
    public static int GetVisualEvolutionLevel(double statValue)
    {
        if (statValue < MinorVisualChangeThreshold) return 0;
        if (statValue < MajorVisualChangeThreshold) return 1;
        if (statValue < MasterVisualChangeThreshold) return 2;
        return 3;
    }
}
