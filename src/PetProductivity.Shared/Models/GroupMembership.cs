namespace PetProductivity.Shared.Models;

public enum GroupRole { Creator = 0, Member = 1 }

/// <summary>
/// N-a-N entre User y Group. Role es informativo (creador vs miembro),
/// NO es propiedad de la mascota: la mascota compartida no tiene dueño.
/// </summary>
public class GroupMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public GroupRole Role { get; set; } = GroupRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
