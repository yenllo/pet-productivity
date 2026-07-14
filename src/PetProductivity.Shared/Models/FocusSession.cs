namespace PetProductivity.Shared.Models;

/// <summary>
/// Sesión de foco cronometrada por el SERVER (anti-trampa AC3): el esfuerzo se mide por tiempo real
/// transcurrido (start→complete), no por auto-reporte. El cliente debe mantener la app en primer plano
/// (si sale, abandona la sesión).
/// </summary>
public class FocusSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid PetId { get; set; }   // vacío = mascota personal
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    // Duración comprometida (min). Al completar, el server exige haber servido ~este tiempo y
    // topa la dificultad por él (no se puede dejar una sesión abierta y reclamar dif 10). 0 = legacy.
    public int TargetMinutes { get; set; }
}
