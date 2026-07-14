using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;

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
}
