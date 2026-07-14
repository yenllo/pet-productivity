namespace PetProductivity.Shared.Models;

/// <summary>
/// Tarea de mascota de GRUPO pendiente de validación social (anti-trampa AC4).
/// La recompensa (ya calculada al enviar) se aplica solo cuando una mayoría de los OTROS miembros aprueba.
/// </summary>
public class TaskApproval
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }
    public Guid PetId { get; set; }
    public Guid RequesterUserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Difficulty { get; set; }
    public string Category { get; set; } = "General";
    public int XpEarned { get; set; }
    public int GoldEarned { get; set; }
    public List<Guid> Approvals { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
