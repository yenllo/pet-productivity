using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PetProductivity.Server.Services;

namespace PetProductivity.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly PetService _petService;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        PetService petService,
        ILogger<TasksController> logger)
    {
        _petService = petService;
        _logger = logger;
    }

    [HttpPost]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> SubmitTask([FromBody] SubmitTaskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("Falta la descripción.");
        // Tope: protege la BD y el prompt de Gemini de payloads gigantes (el rate-limit no basta).
        if (request.Description.Length > 500)
            return BadRequest("La descripción es demasiado larga (máx. 500 caracteres).");

        // 1. Process task (AI Judge + Pet Update). El userId sale del token (no del body). PetId vacío = mascota personal.
        var result = await _petService.ProcessTaskCompletion(
            User.GetUserId(), request.PetId, request.Description, request.Confirmed,
            language: request.Language);

        // T17: el contrato de /api/tasks ES TaskResult (Shared) — mismas claves camelCase que el
        // objeto anónimo que reemplaza (compatible con clientes viejos), pero tipado end-to-end.
        return Ok(result);
    }
}

public class SubmitTaskRequest
{
    public Guid PetId { get; set; }   // vacío = mascota personal
    public bool Confirmed { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = "es"; // T27 #26: idioma del feedback de la IA ("es"|"en")
}
