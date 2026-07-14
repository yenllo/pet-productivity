namespace PetProductivity.Shared.Models;

// T17: contratos REALES de los endpoints de foco — compartidos server↔cliente para que un rename
// sea error de compilación y no una rotura silenciosa en runtime. Las claves JSON no cambian
// (camelCase igual que los objetos anónimos que reemplazan): compatible con clientes viejos.

/// <summary>Respuesta de POST /api/focus/start.</summary>
public class FocusStartResponse
{
    public Guid SessionId { get; set; }
    public DateTime StartedAt { get; set; }
}

/// <summary>Respuesta de POST /api/focus/complete (completado o interrumpido).</summary>
public class FocusCompleteResponse
{
    public int Minutes { get; set; }
    public bool Completed { get; set; }
    public int DifficultyScore { get; set; }
    public int XpEarned { get; set; }
    public int GoldEarned { get; set; }
    public string PetName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public double NewTotalXp { get; set; }
}

/// <summary>Respuesta de POST /api/focus/proof (veredicto de Gemini Vision).</summary>
public class ProofResponse
{
    public Guid ProofId { get; set; }
    public bool Plausible { get; set; }
}

/// <summary>Respuesta de start/join de foco grupal.</summary>
public class GroupFocusInfo
{
    public Guid GroupFocusId { get; set; }
    public Guid FocusSessionId { get; set; }
    public DateTime StartedAt { get; set; }
    public int TargetMinutes { get; set; }
    public Guid PetId { get; set; }
    public string Topic { get; set; } = string.Empty;
}

/// <summary>Estado del foco grupal activo de un grupo.</summary>
public class ActiveGroupFocus
{
    public bool Active { get; set; }
    public Guid GroupFocusId { get; set; }
    public DateTime StartedAt { get; set; }
    public int TargetMinutes { get; set; }
    public Guid PetId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public bool Joined { get; set; }
}

/// <summary>Ítem del historial laboral (personal o de grupo; Username solo en el de grupo).</summary>
public class HistoryItem
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int XpEarned { get; set; }
    public int GoldEarned { get; set; }
    public int AiDifficultyScore { get; set; }
    public Guid? ProofId { get; set; }
    public string ProofVerdict { get; set; } = "none";
    public string? Username { get; set; }
}
