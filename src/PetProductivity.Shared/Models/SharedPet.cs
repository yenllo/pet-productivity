namespace PetProductivity.Shared.Models;

/// <summary>
/// A pet shared by multiple users in a group.
/// Implements anti-polizón (free rider) mechanic with individual affection tracking.
/// </summary>
public class SharedPet : Pet
{
    /// <summary>
    /// Nacimiento de grupo: el huevo no eclosiona por XP sino por voto unánime.
    /// Mientras false, la mascota es un huevo (no recibe tareas). Nace cuando TODOS
    /// los miembros actuales (≥2) presionan "Hacer nacer" → reveal sincronizado.
    /// </summary>
    public bool IsHatched { get; set; }

    /// <summary>UserIds que ya votaron por hacer nacer al huevo (se limpia al nacer).</summary>
    public List<Guid> HatchVotes { get; set; } = new();

    /// <summary>
    /// Tracks affection level (0-100) for each user.
    /// Users who don't contribute get lower affection = grumpy pet.
    /// </summary>
    public Dictionary<Guid, double> UserAffection { get; set; } = new();

    /// <summary>
    /// Update affection for a user based on whether they contributed
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="contributed">Did they complete a task?</param>
    public void UpdateAffection(Guid userId, bool contributed)
    {
        // Initialize if new user
        if (!UserAffection.ContainsKey(userId))
        {
            UserAffection[userId] = 50.0; // Start neutral
        }

        if (contributed)
        {
            // Reward contribution
            UserAffection[userId] = Math.Min(100, UserAffection[userId] + 10);
        }
        else
        {
            // Penalize inactivity (slower decay)
            UserAffection[userId] = Math.Max(0, UserAffection[userId] - 5);
        }
    }

    /// <summary>
    /// Get the pet's mood towards a specific user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Happy, Neutral, or Grumpy</returns>
    public PetMood GetMoodForUser(Guid userId)
    {
        var affection = UserAffection.GetValueOrDefault(userId, 50.0);

        if (affection > 70) return PetMood.Happy;
        if (affection > 30) return PetMood.Neutral;
        return PetMood.Grumpy;
    }

    /// <summary>
    /// Get affection level for a user (0-100)
    /// </summary>
    public double GetAffectionForUser(Guid userId)
    {
        return UserAffection.GetValueOrDefault(userId, 50.0);
    }

    /// <summary>
    /// Decay affection for all users over time (called by background service)
    /// </summary>
    /// <param name="decayAmount">Amount to decrease (default 2)</param>
    public void DecayAllAffection(double decayAmount = 2.0)
    {
        var userIds = UserAffection.Keys.ToList();
        foreach (var userId in userIds)
        {
            UserAffection[userId] = Math.Max(0, UserAffection[userId] - decayAmount);
        }
    }
}
