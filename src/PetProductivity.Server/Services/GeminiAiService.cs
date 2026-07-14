using System.Text.Json;
using System.Text.Json.Nodes;

namespace PetProductivity.Server.Services;

public class GeminiAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiAiService> _logger;

    public GeminiAiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini:ApiKey is missing in appsettings.json");
        _logger = logger;
    }

    public Task<string> GenerateContentAsync(string prompt) =>
        PostAsync(new object[] { new { text = prompt } });

    // Multimodal: texto + imagen (inline_data base64). Mismo endpoint generateContent.
    public Task<string> GenerateFromImageAsync(string prompt, byte[] imageBytes, string mimeType) =>
        PostAsync(new object[]
        {
            new { text = prompt },
            new { inline_data = new { mime_type = mimeType, data = Convert.ToBase64String(imageBytes) } }
        });

    private async Task<string> PostAsync(object[] parts)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={_apiKey}";

        var requestBody = new
        {
            contents = new[]
            {
                new { parts }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, requestBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini API Error. Status: {Status}. Content: {Content}", response.StatusCode, errorContent);
                // Return empty or throw? Ideally let the caller handle it or return empty.
                // The existing services handle exceptions, so throwing is probably fine or returning null/empty.
                // But for now, let's throw to be explicit in logs.
                response.EnsureSuccessStatusCode();
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<JsonObject>();
            
            // Navigate the JSON: candidates -> [0] -> content -> parts -> [0] -> text
            var text = jsonResponse?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            return text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            throw; // Re-throw to let fallback logic in consumers handle it if they have it
        }
    }
}
