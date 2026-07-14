namespace PetProductivity.Shared.Models;

public class AuthRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty; // Optional for Login, required for Register
    public string PetName { get; set; } = "Huevo"; // For Register (T16: default ES)
    public Archetype InitialArchetype { get; set; } = Archetype.Neutral;
    public PetSpecies? InitialSpecies { get; set; }
    // T8: zona IANA del dispositivo (TimeZoneInfo.Local.Id); define el "hoy" del usuario en el server.
    public string TimeZoneId { get; set; } = string.Empty;
}
