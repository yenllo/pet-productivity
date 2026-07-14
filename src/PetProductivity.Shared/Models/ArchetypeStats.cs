namespace PetProductivity.Shared.Models;

/// <summary>
/// Defines which stats belong to each archetype.
/// This ensures consistency between AI categorization and UI display.
/// </summary>
public static class ArchetypeStats
{
    public static readonly Dictionary<Archetype, List<string>> StatsByArchetype = new()
    {
        // Individual Archetypes
        { 
            Archetype.Scholar, 
            new List<string> { "Logic", "Memory", "Eloquence" }
        },
        { 
            Archetype.Technologist, 
            new List<string> { "Code", "Architecture", "Debugging" }
        },
        { 
            Archetype.Creator, 
            new List<string> { "Creativity", "Technique", "Aesthetics" }
        },
        { 
            Archetype.Athlete, 
            new List<string> { "Strength", "Endurance", "Discipline" }
        },
        { 
            Archetype.Executive, 
            new List<string> { "Finance", "Networking", "Management" }
        },
        { 
            Archetype.Neutral, 
            new List<string> { "Cuerpo", "Mente", "Hogar", "Bienestar" }
        },
        
        // Group Archetypes
        { 
            Archetype.Household, 
            new List<string> { "Maintenance", "Care", "Administration" }
        },
        { 
            Archetype.Guild, 
            new List<string> { "Progress", "Support", "Quality" }
        }
    };

    /// <summary>
    /// Get the list of stats for a given archetype
    /// </summary>
    public static List<string> GetStatsForArchetype(Archetype archetype)
    {
        return StatsByArchetype.TryGetValue(archetype, out var stats) 
            ? stats 
            : new List<string> { "General" };
    }

    /// <summary>
    /// Initialize default stats for a pet based on its archetype
    /// </summary>
    public static Dictionary<string, double> InitializeStats(Archetype archetype)
    {
        var stats = new Dictionary<string, double>();
        var statNames = GetStatsForArchetype(archetype);
        
        foreach (var statName in statNames)
        {
            stats[statName] = 0.0; // Start at 0
        }
        
        return stats;
    }

    /// <summary>
    /// Check if a stat name is valid for the given archetype
    /// </summary>
    public static bool IsValidStatForArchetype(Archetype archetype, string statName)
    {
        var validStats = GetStatsForArchetype(archetype);
        return validStats.Contains(statName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reverse lookup: find which archetype "owns" a given stat name.
    /// Returns null if no archetype defines that stat (e.g. "General").
    /// </summary>
    public static Archetype? GetArchetypeForStat(string statName)
    {
        if (string.IsNullOrWhiteSpace(statName)) return null;

        foreach (var kvp in StatsByArchetype)
        {
            if (kvp.Value.Contains(statName, StringComparer.OrdinalIgnoreCase))
                return kvp.Key;
        }

        return null;
    }
}
