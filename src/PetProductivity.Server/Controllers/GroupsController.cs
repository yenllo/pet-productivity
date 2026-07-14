using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PetProductivity.Shared.Models;
using PetProductivity.Server.Services;
using PetProductivity.Server.Hubs;

namespace PetProductivity.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GroupsController : ControllerBase
{
    private readonly GroupService _groups;
    private readonly IHubContext<FamilyHub> _hub;
    private readonly PetService _pets;
    public GroupsController(GroupService groups, IHubContext<FamilyHub> hub, PetService pets)
    {
        _groups = groups;
        _hub = hub;
        _pets = pets;
    }

    // El actor siempre sale del token, no del body/ruta.
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (GroupException ex) { return StatusCode(ex.StatusCode, ex.Message); }
    }

    [HttpPost]
    public Task<IActionResult> Create([FromBody] CreateGroupRequest r) =>
        Run(async () => Ok(await _groups.CreateGroupAsync(User.GetUserId(), r.Name, r.Archetype, r.MaxMembers)));

    [HttpPost("join")]
    public Task<IActionResult> Join([FromBody] JoinByCodeRequest r) =>
        Run(async () => Ok(await _groups.RequestJoinByCodeAsync(r.InviteCode.Trim().ToUpperInvariant(), User.GetUserId())));

    [HttpGet("mine/{userId}")]
    public Task<IActionResult> Mine(Guid userId) =>
        Run(async () => Ok(await _groups.GetMyGroupsAsync(User.GetUserId())));

    [HttpGet("{groupId}")]
    public Task<IActionResult> Detail(Guid groupId) =>
        Run(async () => Ok(await _groups.GetGroupDetailAsync(groupId, User.GetUserId())));

    // Voto para hacer nacer el huevo del grupo. Difunde el progreso o el nacimiento sincronizado.
    [HttpPost("{groupId}/hatch")]
    public Task<IActionResult> Hatch(Guid groupId) =>
        Run(async () =>
        {
            var (hatched, votes, members) = await _groups.VoteToHatchAsync(groupId, User.GetUserId());
            if (hatched)
                await _hub.Clients.Group(FamilyHub.Room(groupId)).SendAsync("PetHatched", groupId);
            else
                await _hub.Clients.Group(FamilyHub.Room(groupId)).SendAsync("HatchProgress", groupId, votes, members);
            return Ok(new { hatched, votes, members });
        });

    [HttpGet("{groupId}/requests")]
    public Task<IActionResult> Requests(Guid groupId) =>
        Run(async () => Ok(await _groups.GetPendingRequestsAsync(groupId, User.GetUserId())));

    [HttpPost("requests/{requestId}/approve")]
    public Task<IActionResult> Approve(Guid requestId, [FromBody] UserIdRequest r) =>
        Run(async () =>
        {
            var group = await _groups.ApproveJoinAsync(requestId, User.GetUserId());
            return group == null ? Accepted() : Ok(group); // 202 pendiente, 200 completado
        });

    // AC4: validar (aprobar) una tarea de grupo pendiente. Al llegar a mayoría, aplica la recompensa.
    [HttpPost("tasks/{approvalId}/approve")]
    public Task<IActionResult> ApproveTask(Guid approvalId) =>
        Run(async () =>
        {
            var (approved, votes, needed) = await _pets.ApproveTaskAsync(approvalId, User.GetUserId());
            return Ok(new { approved, votes, needed });
        });

    [HttpDelete("{groupId}/members/{userId}")]
    public Task<IActionResult> Leave(Guid groupId, Guid userId) =>
        Run(async () => { await _groups.LeaveGroupAsync(groupId, User.GetUserId()); return NoContent(); });
}

public class CreateGroupRequest
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Archetype Archetype { get; set; }
    public int MaxMembers { get; set; } = 6;
}

public class JoinByCodeRequest
{
    public Guid UserId { get; set; }
    public string InviteCode { get; set; } = string.Empty;
}

public class UserIdRequest
{
    public Guid UserId { get; set; }
}
