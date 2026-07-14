namespace PetProductivity.Shared.Models;

/// <summary>
/// Comprobante fotográfico de un foco (AC3 v2 / Gemini Vision): la foto tomada a mitad de sesión + el
/// veredicto de plausibilidad de la IA. La imagen se guarda comprimida (JPEG ~512px) en la BD (bytea).
/// </summary>
public class FocusProof
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }   // FocusSession a la que pertenece (para buscarlo al completar)
    public Guid UserId { get; set; }
    public byte[] Image { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = "image/jpeg";
    public bool Plausible { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
