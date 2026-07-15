namespace PetProductivity.Shared.Models;

public class TaskResult
{
    public Guid TaskId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRevived { get; set; }
    // La mascota sigue cristalizada (no revivió con esta tarea) — distingue este caso del éxito
    // normal sin depender de matchear un texto del Message (antes el cliente buscaba una frase
    // que el server nunca mandaba, y el resultado caía en la superposición de celebración normal).
    public bool IsCrystallized { get; set; }
    public int XpEarned { get; set; }
    public int GoldEarned { get; set; }
    public int DifficultyScore { get; set; } // Renamed from Difficulty for clarity
    public string StatCategory { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;
    public double NewTotalXp { get; set; }
    // T12: frase de acompañamiento (sale del MISMO juicio de IA, ya no de una 2ª llamada).
    public string EmotionalFeedback { get; set; } = string.Empty;

    // Tarea fuera del contexto de la mascota: requiere confirmación (no se commiteó nada).
    public bool NeedsConfirmation { get; set; }
    // Se registró confirmada pese a no encajar: recompensa reducida (×0.25).
    public bool WasReducedReward { get; set; }

    // T13 (solo cliente): sin red — la intención quedó en la cola offline y se enviará al reconectar.
    public bool Queued { get; set; }
}
