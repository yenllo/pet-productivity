namespace PetProductivity.Shared.Models;

/// <summary>
/// Foco grupal sincronizado: varios miembros enfocan a la vez sobre la mascota de grupo. Comparte
/// StartedAt/TargetMinutes (cuenta atrás común); cada participante igual corre su propia FocusSession
/// (alineada) para la cuenta atrás y el comprobante. Esta entidad coordina y lleva la lista de participantes.
/// </summary>
public class GroupFocusSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid PetId { get; set; }       // SharedPet del grupo
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int TargetMinutes { get; set; }
    public string Topic { get; set; } = string.Empty;
    public List<Guid> Participants { get; set; } = new();
}
