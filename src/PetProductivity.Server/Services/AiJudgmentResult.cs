using System.Text.Json.Serialization;

namespace PetProductivity.Server.Services;



public class AiJudgmentResult
{
    [JsonPropertyName("difficulty")]
    public int Difficulty { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("reasoning")]
    public string? Reasoning { get; set; }

    // true si la tarea pertenece de verdad al dominio del arquetipo. Default true:
    // si Gemini lo omite, no bloqueamos.
    [JsonPropertyName("relevant")]
    public bool Relevant { get; set; } = true;

    // 1-10: qué tan plausible/verificable es el claim (anti-mentira). Default 10:
    // si Gemini lo omite, no penalizamos (beneficio de la duda).
    [JsonPropertyName("plausibility")]
    public int Plausibility { get; set; } = 10;

    // T12: frase de acompañamiento en español generada en la MISMA llamada del juicio
    // (antes era una 2ª llamada a Gemini). Si el modelo la omite, se usa el fallback local.
    [JsonPropertyName("feedback")]
    public string? Feedback { get; set; }
}
