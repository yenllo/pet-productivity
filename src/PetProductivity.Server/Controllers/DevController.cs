using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IServiceScopeFactory _scopeFactory;

    public DevController(AppDbContext context, IWebHostEnvironment env, IServiceScopeFactory scopeFactory)
    {
        _context = context;
        _env = env;
        _scopeFactory = scopeFactory;
    }

    [HttpPost("decay-affection")]
    public async Task<IActionResult> DecayAffection()
    {
        if (!_env.IsDevelopment()) return NotFound();
        var n = await AffectionDecayHostedService.RunDecayAsync(_scopeFactory);
        return Ok(new { Message = $"Decaimiento aplicado a {n} mascotas compartidas." });
    }

    [HttpPost("damage")]
    public async Task<IActionResult> ApplyDamage([FromQuery] Guid userId, [FromQuery] double amount)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.UserPet == null) return NotFound("User or Pet not found");

        user.UserPet.ApplyDamage(amount);
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            Message = $"Damaged applied: {amount}. New Health: {user.UserPet.Health}. Status: {user.UserPet.Status}",
            Status = user.UserPet.Status,
            Health = user.UserPet.Health
        });
    }

    // T5: fijar el hambre para verificar el humor visual (Hungry < 30) sin esperar la decadencia.
    [HttpPost("hunger")]
    public async Task<IActionResult> SetHunger([FromQuery] Guid userId, [FromQuery] double value)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.UserPet == null) return NotFound("User or Pet not found");

        user.UserPet.Hunger = Math.Clamp(value, 0, 100);
        await _context.SaveChangesAsync();
        return Ok(new { user.UserPet.Hunger, Condition = user.UserPet.Condition.ToString() });
    }

    [HttpPost("kill")]
    public async Task<IActionResult> KillPet([FromQuery] Guid userId)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.UserPet == null) return NotFound("User or Pet not found");

        // Force kill
        user.UserPet.ApplyDamage(user.UserPet.Health + 100);
        await _context.SaveChangesAsync();

        return Ok(new 
        { 
            Message = "Pet executed. Status is now Crystallized.",
            Status = user.UserPet.Status
        });
    }
}
