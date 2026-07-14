namespace PetProductivity.Shared.Models;

/// <summary>Detalle de un grupo: la mascota compartida, miembros (con ánimo) y solicitudes pendientes.</summary>
public class GroupDetailDto
{
    public Group Group { get; set; } = null!;
    public SharedPet? Pet { get; set; }
    public bool IsDormant { get; set; }
    public bool IsHatched { get; set; }        // ¿ya nació el huevo del grupo?
    public int HatchVotes { get; set; }        // miembros actuales que ya votaron por nacer
    public int MemberCount { get; set; }
    public bool ViewerVoted { get; set; }      // ¿el que pide el detalle ya votó?
    public bool IsFrenzyActive { get; set; }   // snapshot de presencia al abrir
    public List<MemberDto> Members { get; set; } = new();
    public List<JoinRequest> PendingRequests { get; set; } = new();
    public List<PendingTaskDto> PendingTasks { get; set; } = new();  // AC4: validación social
}

public class PendingTaskDto
{
    public Guid Id { get; set; }
    public string RequesterName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Difficulty { get; set; }
    public int XpEarned { get; set; }
    public int Votes { get; set; }
    public int Needed { get; set; }
    public bool ViewerVoted { get; set; }
    public bool ViewerIsRequester { get; set; }
    public bool ViewerCanApprove => !ViewerIsRequester && !ViewerVoted; // botón "Aprobar" visible
    // T6-E: horas restantes hasta la auto-aprobación (countdown visible en la UI).
    public int HoursLeft { get; set; }
}

public class MemberDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public GroupRole Role { get; set; }
    public double Affection { get; set; }
    public PetMood Mood { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Offline; // snapshot de presencia al abrir
}
