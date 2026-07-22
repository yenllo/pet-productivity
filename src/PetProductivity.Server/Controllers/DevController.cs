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

    // Diagnóstico read-only: estado crudo de una mascota por nombre (reloj de decadencia incluido).
    // Nació cazando el bug "invitado recién nacido aparece cristalizado" (2026-07-22).
    [HttpGet("pet-state")]
    public async Task<IActionResult> PetState([FromQuery] string name)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var rows = await _context.Users.Include(u => u.UserPet)
            .Where(u => u.UserPet != null && u.UserPet.Name == name)
            .Select(u => new
            {
                u.Username, u.Email, u.TimeZoneId, u.LastActivityDate, u.CurrentStreak,
                Pet = new
                {
                    u.UserPet!.Id, u.UserPet.Name, u.UserPet.Hunger, u.UserPet.Health,
                    u.UserPet.Status, u.UserPet.LastDecayAt, u.UserPet.GracePeriodExpiry,
                    u.UserPet.TotalXp, u.UserPet.GoldCoins
                }
            })
            .ToListAsync();
        return Ok(rows);
    }

    // Contraparte de kill: revive por la vía Fénix (hazaña 9+) y repone el hambre para dejar
    // una mascota de prueba usable (revivir con hambre 0 la manda de vuelta al cristal).
    [HttpPost("revive")]
    public async Task<IActionResult> RevivePet([FromQuery] Guid petId)
    {
        if (!_env.IsDevelopment()) return NotFound();

        var pet = await _context.Pets.FindAsync(petId);
        if (pet == null) return NotFound("Pet not found");

        if (!pet.TryRevive(Pet.ReviveDifficulty)) return BadRequest("La mascota no está cristalizada.");
        pet.Hunger = 100;
        await _context.SaveChangesAsync();
        return Ok(new { pet.Name, pet.Status, pet.Health, pet.Hunger });
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
