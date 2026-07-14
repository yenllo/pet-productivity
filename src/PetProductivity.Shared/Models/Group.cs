namespace PetProductivity.Shared.Models;

/// <summary>
/// Represents a group of users working together with a shared pet
/// </summary>
public class Group
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // ponytail: superseded by GroupMembership (single source of truth). Column kept
    // (no destructive drop); not read/written in Fase 3 logic.
    public List<Guid> MemberIds { get; set; } = new();

    // Invitación / capacidad
    public string InviteCode { get; set; } = string.Empty; // 6 chars A-Z0-9, único
    public int MaxMembers { get; set; } = 6;               // 2 = pareja .. 6

    // Shared Pet (FK real → Pets, opcional para tolerar filas legadas)
    public Guid SharedPetId { get; set; }
    public SharedPet? SharedPet { get; set; }

    // Combo System
    public double CurrentMultiplier { get; set; } = 1.0;
    public DateTime? ComboExpiry { get; set; }

    // Archetype for the group (determines shared pet's stats)
    public Archetype GroupArchetype { get; set; }
}

/// <summary>
/// Mood of the pet towards a specific user (for anti-polizón mechanic)
/// </summary>
public enum PetMood
{
    Happy = 0,      // User is contributing well
    Neutral = 1,    // User is contributing moderately
    Grumpy = 2      // User is not contributing (free rider)
}
