namespace PetProductivity.Shared.Models;

/// <summary>Presencia en vivo de un miembro de familia (SignalR Presence).</summary>
public class MemberPresence
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public SyncStatus Status { get; set; }
}
