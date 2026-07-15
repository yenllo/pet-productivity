using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

// T14-C1: borrado de cuenta (Google lo exige para publicar apps con cuentas). Reutiliza la salida
// de grupo existente (mantiene consistente la capa social: votos, afecto, y borra el grupo con su
// mascota compartida si queda vacío) y después elimina todo rastro propio del usuario.
public class AccountService
{
    private readonly AppDbContext _context;
    private readonly GroupService _groups;

    public AccountService(AppDbContext context, GroupService groups)
    {
        _context = context;
        _groups = groups;
    }

    public async Task<bool> DeleteAsync(Guid userId)
    {
        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return false;

        // 1) Capa social: salir de cada grupo con la lógica ya probada (T24).
        var groupIds = await _context.GroupMemberships.Where(m => m.UserId == userId)
            .Select(m => m.GroupId).ToListAsync();
        foreach (var gid in groupIds)
            await _groups.LeaveGroupAsync(gid, userId);

        // 2) Solicitudes pendientes a grupos donde aún NO era miembro (LeaveGroup no las ve).
        _context.JoinRequests.RemoveRange(_context.JoinRequests.Where(j => j.RequesterUserId == userId));

        // 3) Focos grupales activos: la lista de participantes es JSON, no FK.
        //    ponytail: tabla efímera y diminuta (solo sesiones en curso), leerla entera está bien.
        foreach (var s in await _context.GroupFocusSessions.ToListAsync())
        {
            if (!s.Participants.Remove(userId)) continue;
            if (s.Participants.Count == 0) _context.GroupFocusSessions.Remove(s);
        }

        // 4) Datos propios: tareas (incluidas las hechas a mascotas de grupo), focos, fotos, sesiones.
        _context.TaskItems.RemoveRange(_context.TaskItems.Where(t => t.UserId == userId));
        _context.FocusSessions.RemoveRange(_context.FocusSessions.Where(f => f.UserId == userId));
        _context.FocusProofs.RemoveRange(_context.FocusProofs.Where(p => p.UserId == userId));
        _context.RefreshTokens.RemoveRange(_context.RefreshTokens.Where(r => r.UserId == userId));

        _context.Users.Remove(user);
        if (user.UserPet != null) _context.Pets.Remove(user.UserPet);
        await _context.SaveChangesAsync();
        return true;
    }

    // T4-A: retirar a un Maestro (prestigio/generaciones). El usuario decide: no se le quita nada.
    // El Maestro pasa a la vitrina de legado (RetiredPets) y la MISMA entidad renace como cría fresca
    // Gen+1 — se preserva oro/inventario (viven en User); solo se reinicia el crecimiento (XP/stats).
    public enum RetireOutcome { Ok, NotFound, NotMaster }

    public async Task<(RetireOutcome Outcome, User? User)> RetireAsync(Guid userId, string newName)
    {
        var user = await _context.Users.Include(u => u.UserPet).FirstOrDefaultAsync(u => u.Id == userId);
        var pet = user?.UserPet;
        if (user == null || pet == null) return (RetireOutcome.NotFound, null);

        // Criterio 4: no se puede retirar antes de Maestro (la etapa se deriva del XP, verdad del server).
        if (pet.EvolutionStage != EvolutionStage.Master) return (RetireOutcome.NotMaster, null);

        user.RetiredPets.Add(new RetiredPet
        {
            Name = pet.Name,
            Species = pet.Species,
            FinalTotalXp = pet.TotalXp,
            Generation = pet.Generation,
            RetiredAt = DateTime.UtcNow,
        });

        // Renace como cría fresca (mismos umbrales que un pet nuevo → criterio 5: no hereda XP que
        // salte etapas). Personal = Neutral por diseño; especie nueva aleatoria (cosmético).
        var name = (newName ?? "").Trim();
        if (name.Length == 0) name = "Cría";
        if (name.Length > 24) name = name[..24];
        pet.Name = name;
        pet.Species = (PetSpecies)Random.Shared.Next(0, 3);
        pet.CurrentArchetype = Archetype.Neutral;
        pet.Stats = ArchetypeStats.InitializeStats(Archetype.Neutral);
        pet.TotalXp = 50;                 // igual que un registro nuevo (nace al borde de Cría)
        pet.Generation += 1;
        pet.Hunger = 100;
        pet.RevivalProgress = 0;
        pet.LastRevivalCreditDay = null;
        pet.LastDecayAt = null;
        pet.Heal(pet.MaxHealth);          // cría sana (Health tiene setter privado; Heal la topa)

        await _context.SaveChangesAsync();
        user.Password = string.Empty;
        return (RetireOutcome.Ok, user);
    }
}
