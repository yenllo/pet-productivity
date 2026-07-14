namespace PetProductivity.Server.Services;

public interface IAiService
{
    Task<string> GenerateContentAsync(string prompt);

    // Multimodal (Gemini Vision): juzga una imagen + prompt. Devuelve el texto de la respuesta.
    Task<string> GenerateFromImageAsync(string prompt, byte[] imageBytes, string mimeType);
}
