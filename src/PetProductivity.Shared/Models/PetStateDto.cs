namespace PetProductivity.Shared.Models;

/// <summary>Estado de una mascota compartida difundido en vivo (SignalR PetUpdate).</summary>
public class PetStateDto
{
    public double Health { get; set; }
    public double MaxHealth { get; set; }
    public double TotalXp { get; set; }
    public double Hunger { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<Guid, double> Affection { get; set; } = new();

    public static PetStateDto From(SharedPet p) => new()
    {
        Health = p.Health,
        MaxHealth = p.MaxHealth,
        TotalXp = p.TotalXp,
        Hunger = p.Hunger,
        Name = p.Name,
        Affection = new Dictionary<Guid, double>(p.UserAffection)
    };
}
