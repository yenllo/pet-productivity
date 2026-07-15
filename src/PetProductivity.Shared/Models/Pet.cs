using System.Text.Json.Serialization;

namespace PetProductivity.Shared.Models;

public enum PetStatus
{
    Alive = 0,
    Crystallized = 1
    // Future proofing: Hibernating, Ascended, etc.
}

// T5: humor físico DERIVADO de Hunger/Health/Status — de solo lectura, sin estado nuevo que
// balancear. OJO: distinto de PetMood (afecto de la mascota de grupo hacia UN usuario, anti-polizón).
public enum PetCondition
{
    Normal = 0,
    Happy = 1,
    Hungry = 2,
    Weak = 3,
    Crystal = 4
}

public class Pet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Huevo"; // T16: default visible en español
    
    // Estado Físico
    public double Hunger { get; set; } = 100; // 0-100 (Baja con el tiempo)
    // T5: columna MUERTA — nada la escribe y la UI ya no la muestra (el humor real es Condition).
    // Borrarla cuando una migración futura pase por aquí; dejarla dormida es gratis.
    public double Happiness { get; set; }

    // T5: umbrales del humor. HungryAt = mismo umbral que el push de hambre (T2) — una sola historia.
    public const int HungryAt = 30;
    public const int WeakAt = 40;

    // Prioridad: cristal > débil > hambrienta > feliz. Calculado: siempre coherente con las mecánicas.
    [JsonIgnore] public PetCondition Condition =>
        Status == PetStatus.Crystallized ? PetCondition.Crystal :
        Health < WeakAt ? PetCondition.Weak :
        Hunger < HungryAt ? PetCondition.Hungry :
        Hunger > 60 && Health > 70 ? PetCondition.Happy :
        PetCondition.Normal;
    
    // La Evolución depende de los Stats
    public Archetype CurrentArchetype { get; set; }

    // Especie visual (solo cosmética, asignada aleatoriamente al nacer). Define el sprite.
    public PetSpecies Species { get; set; } = PetSpecies.Sprout;
    
    // Stats Dinámicos (Ej: "Logic": 50.0, "Strength": 10.0)
    // Esto es lo que modifica la apariencia
    public Dictionary<string, double> Stats { get; set; } = new();
    
    // Total XP for evolution stage calculation
    public double TotalXp { get; set; } = 0;

    // T4-A: nº de generación (prestigio). 1 = la primera mascota; sube al retirar un Maestro y nacer
    // una cría nueva. Los ancestros retirados viven en User.RetiredPets.
    public int Generation { get; set; } = 1;
    
    // Evolution stage (calculated from TotalXp)
    public EvolutionStage EvolutionStage => PetEvolution.CalculateEvolutionStage(TotalXp);
    
    // T10: hasta cuándo se aplicó la decadencia (lazy — ver DecayMath). Null = aún sin tocar.
    public DateTime? LastDecayAt { get; set; }

    // Fénix (T19): umbrales nombrados del ciclo cristal→revivir.
    public const int ReviveDifficulty = 9;          // "mega tarea" que rompe el cristal
    public const double ReviveHealthFraction = 0.2; // revive con este % de la vida
    public const int GraceHours = 24;               // escudo de gracia tras revivir

    // T3-A: vía acumulativa — esfuerzo real (foco o tarea ≥5) en días DISTINTOS agrieta el cristal.
    public const int RevivalCreditDifficulty = 5;   // tarea que cuenta como grieta
    public const int RevivalDaysNeeded = 3;         // grietas para revivir
    public int RevivalProgress { get; set; }
    public DateTime? LastRevivalCreditDay { get; set; } // día local ya acreditado (token de LocalDay)

    public int GoldCoins { get; set; }

    // Phoenix Mechanic
    // [JsonInclude]: setter privado → EF lo puebla por backing-field, pero System.Text.Json (cliente)
    // lo ignoraría y la mascota personal llegaría siempre "viva". Con esto el cliente ve el estado real.
    [JsonInclude] public PetStatus Status { get; private set; } = PetStatus.Alive;
    [JsonInclude] public double Health { get; private set; } = 100;
    [JsonInclude] public double MaxHealth { get; private set; } = 100;
    [JsonInclude] public DateTime? GracePeriodExpiry { get; private set; }

    public void ApplyDamage(double damage)
    {
        if (Status == PetStatus.Crystallized) return; // Ya es piedra, no sufre.
        
        // Grace Shield check
        if (GracePeriodExpiry.HasValue && DateTime.UtcNow < GracePeriodExpiry.Value) return;

        Health -= damage;
        if (Health <= 0)
        {
            Health = 0;
            Crystallize();
        }
    }

    private void Crystallize()
    {
        Status = PetStatus.Crystallized;
        GracePeriodExpiry = null; // Reset grace period on death
    }

    public bool TryRevive(int taskDifficulty)
    {
        if (Status != PetStatus.Crystallized) return false;

        // DEFINICIÓN DE MEGA TAREA (T3-F: sigue siendo la vía instantánea):
        // Una tarea que la IA juzgue con dificultad ReviveDifficulty o más.
        if (taskDifficulty >= ReviveDifficulty)
        {
            Revive();
            return true;
        }

        return false;
    }

    // T3-A: suma 1 grieta si el día local (token ya resuelto por el caller) aún no acreditó.
    // Devuelve true si con esta grieta el cristal se rompió (revivió).
    public bool AddRevivalCredit(DateTime localDayToken)
    {
        if (Status != PetStatus.Crystallized) return false;
        if (LastRevivalCreditDay == localDayToken) return false; // mismo día: no acumula
        LastRevivalCreditDay = localDayToken;
        RevivalProgress++;
        if (RevivalProgress < RevivalDaysNeeded) return false;
        Revive();
        return true;
    }

    private void Revive()
    {
        Status = PetStatus.Alive;
        Health = MaxHealth * ReviveHealthFraction; // revive débil
        GracePeriodExpiry = DateTime.UtcNow.AddHours(GraceHours);
        RevivalProgress = 0;          // T3: el ciclo parte limpio para una próxima muerte
        LastRevivalCreditDay = null;
    }

    public void Heal(double amount)
    {
        if (Status == PetStatus.Crystallized) return; // No puedes curar una piedra.
        
        Health += amount;
        if (Health > MaxHealth) Health = MaxHealth;
    }

    // ========== STATS EVOLUTION METHODS ==========

    /// <summary>
    /// Get the value of a specific stat
    /// </summary>
    public double GetStatValue(string statName)
    {
        return Stats.TryGetValue(statName, out var value) ? value : 0.0;
    }

    /// <summary>
    /// Add XP to a specific stat and update total XP
    /// </summary>
    public void AddStatXp(string statName, double amount)
    {
        if (Status == PetStatus.Crystallized) return; // Cristales no crecen
        
        // Validate stat belongs to archetype
        if (!ArchetypeStats.IsValidStatForArchetype(CurrentArchetype, statName))
        {
            // Fallback to "General" if invalid
            statName = "General";
        }

        if (!Stats.ContainsKey(statName))
        {
            Stats[statName] = 0;
        }

        Stats[statName] += amount;
        TotalXp += amount;
    }

    /// <summary>
    /// Get the dominant stat (highest value) for visual evolution
    /// </summary>
    public string GetDominantStat()
    {
        return PetEvolution.GetDominantStat(Stats);
    }

    /// <summary>
    /// Get visual evolution level for the dominant stat
    /// </summary>
    public int GetVisualEvolutionLevel()
    {
        var dominantStat = GetDominantStat();
        if (dominantStat == "None") return 0;
        
        var statValue = GetStatValue(dominantStat);
        return PetEvolution.GetVisualEvolutionLevel(statValue);
    }

    /// <summary>
    /// Initialize stats based on archetype
    /// </summary>
    public void InitializeStatsForArchetype()
    {
        Stats = ArchetypeStats.InitializeStats(CurrentArchetype);
    }
}
