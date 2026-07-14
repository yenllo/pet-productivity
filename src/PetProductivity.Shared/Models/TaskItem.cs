namespace PetProductivity.Shared.Models;

public class TaskItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty; // Input del usuario
    public bool IsCompleted { get; set; }

    // Datos generados por la IA
    public int AiDifficultyScore { get; set; } // 1-10
    public string AiStatCategory { get; set; } = "General"; // Ej: "Logic"

    // Historial / auditoría (quién, a qué mascota, recompensa y cuándo)
    public Guid UserId { get; set; }
    public Guid PetId { get; set; }
    public int XpEarned { get; set; }
    public int GoldEarned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Comprobante (Gemini Vision): foto asociada + veredicto. "none" = sin foto, "ok" = ✓, "fail" = ✗.
    public Guid? ProofId { get; set; }
    public string ProofVerdict { get; set; } = "none";
}
