namespace PetProductivity.Shared.Models;

/// <summary>
/// Solicitud pendiente de unirse a un grupo vía código de invitación.
/// Se aprueba solo cuando TODOS los miembros actuales han aceptado (unanimidad).
/// </summary>
public class JoinRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid RequesterUserId { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public List<Guid> Approvals { get; set; } = new();

    // Nombre del solicitante (no se persiste; se rellena al armar el detalle del grupo).
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string RequesterName { get; set; } = string.Empty;
}
