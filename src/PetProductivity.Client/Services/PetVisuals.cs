using PetProductivity.Shared.Models;

namespace PetProductivity.Client.Services;

/// <summary>
/// Sprite de la mascota personal según especie + etapa (starters Moko: planta/fuego/agua ×
/// cría/adulto/maestro). Huevo y cristalización tienen sus propios sprites.
/// </summary>
public static class PetVisuals
{
    public static string SpriteFor(Pet? pet)
    {
        if (pet == null) return "pet_egg.png";
        if (pet.Status == PetStatus.Crystallized) return "pet_crystal.png";
        if (pet.EvolutionStage == EvolutionStage.Egg) return "pet_egg.png";
        return $"pet_{pet.Species.ToString().ToLowerInvariant()}_{StageSuffix(pet.EvolutionStage)}.png";
    }

    // Sprite de una etapa concreta de una especie (para previsualizar la evolución).
    public static string SpriteFor(PetSpecies species, EvolutionStage stage) =>
        $"pet_{species.ToString().ToLowerInvariant()}_{StageSuffix(stage)}.png";

    // Mascota de GRUPO: por ahora reusa las especies iniciales (1 por arquetipo, determinista).
    // ÚNICO punto a cambiar cuando haya arte de grupo dedicado.
    public static string GroupSprite(Archetype archetype, EvolutionStage stage) =>
        SpriteFor((PetSpecies)((int)archetype % 3), stage);

    // T16: claves internas de stats (inglés, así viven en BD y las juzga la IA) → nombre visible en
    // español. SOLO display: los datos no cambian, cero migración. Las del arquetipo Neutral ya son ES.
    private static readonly Dictionary<string, string> StatNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Logic"] = "Lógica", ["Memory"] = "Memoria", ["Eloquence"] = "Elocuencia",
        ["Code"] = "Código", ["Architecture"] = "Arquitectura", ["Debugging"] = "Depuración",
        ["Creativity"] = "Creatividad", ["Technique"] = "Técnica", ["Aesthetics"] = "Estética",
        ["Strength"] = "Fuerza", ["Endurance"] = "Resistencia", ["Discipline"] = "Disciplina",
        ["Finance"] = "Finanzas", ["Networking"] = "Contactos", ["Management"] = "Gestión",
        ["Maintenance"] = "Mantención", ["Care"] = "Cuidado", ["Administration"] = "Administración",
        ["Progress"] = "Progreso", ["Support"] = "Apoyo", ["Quality"] = "Calidad",
    };

    public static string StatDisplayName(string key) =>
        L.Lang == "en" ? key : (StatNames.TryGetValue(key, out var es) ? es : key); // #26: en EN la clave interna YA es inglés

    // T5: burbuja de humor — una sola tabla para Dashboard y detalle de grupo (humores consistentes).
    // Normal = sin burbuja; Crystal ya tiene overlay/sprite propio.
    public static string MoodEmoji(Pet? pet) => pet?.Condition switch
    {
        PetCondition.Happy => "✨",
        PetCondition.Hungry => "🥺",
        PetCondition.Weak => "💔",
        _ => ""
    };

    private static string StageSuffix(EvolutionStage stage) => stage switch
    {
        EvolutionStage.Master => "master",
        EvolutionStage.Adult => "adult",
        _ => "baby"   // Cría (y cualquier estado post-huevo)
    };

    // Nombre localizado de la etapa — única fuente para Dashboard y Stats (antes StatsPage tenía su
    // propio "Nivel" (TotalXp/1000) desconectado de esta etapa real; ver tareas/30).
    public static string StageName(EvolutionStage stage) => stage switch
    {
        EvolutionStage.Egg => L.T("Huevo"),
        EvolutionStage.Baby => L.T("Cría"),
        EvolutionStage.Adult => L.T("Adulto"),
        EvolutionStage.Master => L.T("Maestro"),
        _ => ""
    };

    // Progreso (0-1) dentro de la etapa actual, usando los mismos umbrales que EvolutionStage.
    public static double StageProgress(EvolutionStage stage, double totalXp)
    {
        var (lo, hi) = stage switch
        {
            EvolutionStage.Egg => (0.0, PetEvolution.EggTobabyThreshold),
            EvolutionStage.Baby => (PetEvolution.EggTobabyThreshold, PetEvolution.BabyToAdultThreshold),
            EvolutionStage.Adult => (PetEvolution.BabyToAdultThreshold, PetEvolution.AdultToMasterThreshold),
            _ => (0.0, 0.0) // Master: ya en el tope, sin techo real
        };
        return hi > lo ? Math.Clamp((totalXp - lo) / (hi - lo), 0, 1) : 1;
    }
}
