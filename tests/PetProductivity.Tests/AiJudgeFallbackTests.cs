using Microsoft.Extensions.Logging.Abstractions;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;
using Xunit;

namespace PetProductivity.Tests;

public class AiJudgeFallbackTests
{
    // IA que no responde -> fuerza la lógica de respaldo.
    private class EmptyAi : IAiService
    {
        public Task<string> GenerateContentAsync(string prompt) => Task.FromResult("");
        public Task<string> GenerateFromImageAsync(string prompt, byte[] imageBytes, string mimeType) => Task.FromResult("");
    }

    [Fact]
    public async Task EvaluateTask_SinIA_CaeAlFallback_Determinista()
    {
        var svc = new AiJudgeService(NullLogger<AiJudgeService>.Instance, new EmptyAi());

        var (difficulty, category, relevant, plausibility, feedback) = await svc.EvaluateTaskAsync(
            "estudiar dos horas de matematicas", Archetype.Neutral);

        Assert.InRange(difficulty, 1, 10);
        Assert.False(string.IsNullOrEmpty(category));
        Assert.True(relevant); // el fallback da beneficio de la duda
        Assert.InRange(plausibility, 1, 10);
        Assert.False(string.IsNullOrWhiteSpace(feedback)); // T12: siempre hay frase, incluso sin IA
    }

    [Fact]
    public async Task Fallback_NoEsExplotablePorLargoDeTexto()
    {
        var svc = new AiJudgeService(NullLogger<AiJudgeService>.Instance, new EmptyAi());

        // Antes: dificultad = largo/5 → un texto largo daba 10. Ahora el fallback es bajo y fijo.
        var (difficulty, _, _, _, _) = await svc.EvaluateTaskAsync(new string('a', 500), Archetype.Neutral);

        Assert.True(difficulty <= 3, $"El fallback no debe inflarse con el largo (fue {difficulty}).");
    }

    // ---- T12: una sola llamada trae juicio + feedback ----

    private class CannedAi : IAiService
    {
        private readonly string _json;
        public CannedAi(string json) => _json = json;
        public Task<string> GenerateContentAsync(string prompt) => Task.FromResult(_json);
        public Task<string> GenerateFromImageAsync(string prompt, byte[] imageBytes, string mimeType) => Task.FromResult(_json);
    }

    [Fact]
    public async Task Juicio_ConCampoFeedback_LoDevuelve()
    {
        var svc = new AiJudgeService(NullLogger<AiJudgeService>.Instance, new CannedAi(
            """{"difficulty":6,"category":"Mente","reasoning":"ok","relevant":true,"plausibility":8,"feedback":"Dos horas de cálculo sostenido; estira la espalda."}"""));

        var r = await svc.EvaluateTaskAsync("estudié cálculo 2 horas", Archetype.Neutral);

        Assert.Equal(6, r.Difficulty);
        Assert.Equal("Dos horas de cálculo sostenido; estira la espalda.", r.Feedback);
    }

    [Fact]
    public async Task Juicio_SinCampoFeedback_RespuestaVieja_NoRompe_YUsaFallbackEnEspanol()
    {
        var svc = new AiJudgeService(NullLogger<AiJudgeService>.Instance, new CannedAi(
            """{"difficulty":5,"category":"Mente","reasoning":"ok","relevant":true,"plausibility":8}"""));

        var r = await svc.EvaluateTaskAsync("estudié cálculo", Archetype.Neutral);

        Assert.Equal(5, r.Difficulty);
        Assert.False(string.IsNullOrWhiteSpace(r.Feedback));
        Assert.Contains("hidratado", r.Feedback); // rama dificultad 5-6 del fallback local en español
    }
}
