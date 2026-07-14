using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Controllers;

// Demo pública del Tasador para la landing (wwwroot/demo.html): juzga un texto con la IA real
// y devuelve lo que habría pagado, SIN tocar la BD ni requerir cuenta. La única defensa que
// importa aquí es el costo de Gemini: rate-limit "demo" por IP (más estricto que "ai") + tope
// de input. El prompt del juez ya es anti-inyección (ver AiJudgeService).
[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
    public const int MaxDescriptionLength = 280;

    private readonly AiJudgeService _judge;

    public DemoController(AiJudgeService judge)
    {
        _judge = judge;
    }

    public class DemoJudgeRequest
    {
        public string Description { get; set; } = string.Empty;
    }

    [AllowAnonymous]
    [EnableRateLimiting("demo")]
    [HttpPost("judge")]
    public async Task<IActionResult> Judge([FromBody] DemoJudgeRequest request)
    {
        var description = (request.Description ?? "").Trim();
        if (description.Length == 0) return BadRequest(new { error = "Describe a task first." });
        if (description.Length > MaxDescriptionLength)
            return BadRequest(new { error = $"Keep it under {MaxDescriptionLength} characters." });

        var (difficulty, category, relevant, plausibility, feedback) =
            await _judge.EvaluateTaskAsync(description, Archetype.Neutral, "en");

        // Recompensa en condiciones neutras (sin ritual, sin frenesí, primera tarea del día).
        var (xp, gold) = RewardMath.Compute(difficulty, plausibility, 1.0,
            outOfContext: false, duplicate: false, tasksToday: 0, frenzy: false, rewardMultiplier: 1.0);

        // Las stats Neutral son claves en español dentro del juego; la landing es en inglés.
        category = category switch
        {
            "Cuerpo" => "Body", "Mente" => "Mind", "Hogar" => "Home", "Bienestar" => "Wellbeing",
            _ => category
        };

        return Ok(new { difficulty, category, relevant, plausibility, feedback, xp, gold });
    }
}
