using Microsoft.EntityFrameworkCore;
using PetProductivity.Shared.Models;
using PetProductivity.Server.Data;

namespace PetProductivity.Server.Services;

/// <summary>Error de dominio con código HTTP; el controlador lo mapea.</summary>
public class GroupException : Exception
{
    public int StatusCode { get; }
    public GroupException(int statusCode, string message) : base(message) => StatusCode = statusCode;
}

public class GroupService
{
    private readonly AppDbContext _context;
    private readonly PresenceService _presence;
    private readonly PetWriteLock _lock;
    public GroupService(AppDbContext context, PresenceService presence, PetWriteLock petLock)
    {
        _context = context;
        _presence = presence;
        _lock = petLock;
    }

    // Sin O/0 ni I/1 para evitar confusión al dictar el código.
    private const string CodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static string NewCode() =>
        new(Enumerable.Range(0, 6).Select(_ => CodeChars[Random.Shared.Next(CodeChars.Length)]).ToArray());

    // Anti-IDOR: sin esto, cualquier autenticado con el groupId (incluido un ex-miembro) leía todo el grupo.
    private async Task EnsureMemberAsync(Guid groupId, Guid userId)
    {
        if (!await _context.GroupMemberships.AnyAsync(m => m.GroupId == groupId && m.UserId == userId))
            throw new GroupException(403, "No eres miembro de este grupo");
    }

    public async Task<Group> CreateGroupAsync(Guid creatorId, string name, Archetype archetype, int maxMembers)
    {
        if (!await _context.Users.AnyAsync(u => u.Id == creatorId))
            throw new GroupException(404, "Usuario no encontrado");

        maxMembers = Math.Clamp(maxMembers, 2, 6);
        name = string.IsNullOrWhiteSpace(name) ? "Familia" : name.Trim();

        var pet = new SharedPet
        {
            Name = name,
            CurrentArchetype = archetype,
            Stats = ArchetypeStats.InitializeStats(archetype),
            TotalXp = 50, // nace Bebé (sprite visible) una vez funcional
            UserAffection = new() { [creatorId] = 50.0 }
        };

        string code;
        do { code = NewCode(); } while (await _context.Groups.AnyAsync(g => g.InviteCode == code));

        var group = new Group
        {
            Name = name,
            GroupArchetype = archetype,
            MaxMembers = maxMembers,
            InviteCode = code,
            SharedPet = pet,
            SharedPetId = pet.Id
        };

        _context.SharedPets.Add(pet);
        _context.Groups.Add(group);
        _context.GroupMemberships.Add(new GroupMembership
        {
            GroupId = group.Id,
            UserId = creatorId,
            Role = GroupRole.Creator
        });
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task<JoinRequest> RequestJoinByCodeAsync(string inviteCode, Guid requesterId)
    {
        var group = await _context.Groups.FirstOrDefaultAsync(g => g.InviteCode == inviteCode);
        if (group == null) throw new GroupException(404, "Código inválido");

        if (await _context.GroupMemberships.AnyAsync(m => m.GroupId == group.Id && m.UserId == requesterId))
            throw new GroupException(409, "Ya eres miembro de este grupo");

        var memberCount = await _context.GroupMemberships.CountAsync(m => m.GroupId == group.Id);
        if (memberCount >= group.MaxMembers)
            throw new GroupException(409, "El grupo está lleno");

        var existing = await _context.JoinRequests
            .FirstOrDefaultAsync(j => j.GroupId == group.Id && j.RequesterUserId == requesterId);
        if (existing != null) return existing; // idempotente

        var req = new JoinRequest { GroupId = group.Id, RequesterUserId = requesterId };
        _context.JoinRequests.Add(req);
        await _context.SaveChangesAsync();
        return req;
    }

    /// <summary>Aprueba; si TODOS los miembros actuales han aprobado, agrega al solicitante. Devuelve el grupo si se completó, null si sigue pendiente.</summary>
    public async Task<Group?> ApproveJoinAsync(Guid requestId, Guid approverId)
    {
        // T24: serializa votos concurrentes sobre la MISMA solicitud (sin esto, last-write-wins
        // pierde votos — mismo defecto que T11-D2 en las aprobaciones de tarea).
        using var _ = await _lock.AcquireAsync(requestId);
        var req = await _context.JoinRequests.FirstOrDefaultAsync(j => j.Id == requestId);
        if (req == null) throw new GroupException(404, "Solicitud no encontrada");

        var members = await _context.GroupMemberships
            .Where(m => m.GroupId == req.GroupId).Select(m => m.UserId).ToListAsync();
        if (!members.Contains(approverId))
            throw new GroupException(403, "No eres miembro de este grupo");

        if (!req.Approvals.Contains(approverId))
            req.Approvals.Add(approverId);

        // Unanimidad recomputada contra los miembros ACTUALES (tolera cambios durante la solicitud).
        bool unanimous = members.All(m => req.Approvals.Contains(m));
        if (!unanimous)
        {
            await _context.SaveChangesAsync();
            return null;
        }

        return await CompleteJoinAsync(req);
    }

    // Completa una solicitud unánime: valida cupo, agrega al miembro y borra la solicitud.
    // Compartido entre la aprobación directa y la unanimidad pasiva al salir un miembro (T24).
    private async Task<Group> CompleteJoinAsync(JoinRequest req)
    {
        var group = await _context.Groups.Include(g => g.SharedPet).FirstAsync(g => g.Id == req.GroupId);
        // Recontar justo antes de añadir (evita exceder MaxMembers por aprobaciones concurrentes).
        var currentCount = await _context.GroupMemberships.CountAsync(m => m.GroupId == group.Id);
        if (currentCount >= group.MaxMembers)
        {
            _context.JoinRequests.Remove(req);
            await _context.SaveChangesAsync();
            throw new GroupException(409, "El grupo está lleno");
        }

        _context.GroupMemberships.Add(new GroupMembership
        {
            GroupId = group.Id,
            UserId = req.RequesterUserId,
            Role = GroupRole.Member
        });
        if (group.SharedPet != null)
            group.SharedPet.UserAffection[req.RequesterUserId] = 50.0;
        _context.JoinRequests.Remove(req);
        await _context.SaveChangesAsync();
        return group;
    }

    public async Task<List<Group>> GetMyGroupsAsync(Guid userId)
    {
        var groupIds = await _context.GroupMemberships
            .Where(m => m.UserId == userId).Select(m => m.GroupId).ToListAsync();
        return await _context.Groups.Include(g => g.SharedPet)
            .Where(g => groupIds.Contains(g.Id)).ToListAsync();
    }

    public async Task<GroupDetailDto> GetGroupDetailAsync(Guid groupId, Guid viewerId)
    {
        await EnsureMemberAsync(groupId, viewerId);
        var group = await _context.Groups.Include(g => g.SharedPet).FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) throw new GroupException(404, "Grupo no encontrado");

        var memberships = await _context.GroupMemberships.Where(m => m.GroupId == groupId).ToListAsync();
        var userIds = memberships.Select(m => m.UserId).ToList();
        var names = await _context.Users.Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username);
        var pet = group.SharedPet;
        var live = _presence.GetPresence(groupId).ToDictionary(p => p.UserId, p => p.Status);
        var votes = pet == null ? 0 : memberships.Count(m => pet.HatchVotes.Contains(m.UserId));

        // Solicitudes de unión con el nombre del solicitante (que aún no es miembro).
        var requests = await _context.JoinRequests.Where(j => j.GroupId == groupId).ToListAsync();
        if (requests.Count > 0)
        {
            var reqIds = requests.Select(r => r.RequesterUserId).ToList();
            var reqNames = await _context.Users.Where(u => reqIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);
            foreach (var rq in requests)
                rq.RequesterName = reqNames.GetValueOrDefault(rq.RequesterUserId, "Invitado");
        }

        return new GroupDetailDto
        {
            Group = group,
            Pet = pet,
            IsDormant = memberships.Count < 2,
            IsHatched = pet?.IsHatched ?? false,
            HatchVotes = votes,
            MemberCount = memberships.Count,
            ViewerVoted = pet?.HatchVotes.Contains(viewerId) ?? false,
            IsFrenzyActive = _presence.IsFrenzyActive(groupId),
            Members = memberships.Select(m => new MemberDto
            {
                UserId = m.UserId,
                Username = names.GetValueOrDefault(m.UserId, "Invitado"),
                Role = m.Role,
                Affection = pet?.GetAffectionForUser(m.UserId) ?? 50,
                Mood = pet?.GetMoodForUser(m.UserId) ?? PetMood.Neutral,
                Status = live.GetValueOrDefault(m.UserId, SyncStatus.Offline)
            }).ToList(),
            PendingRequests = requests,
            PendingTasks = (await _context.TaskApprovals.Where(t => t.GroupId == groupId).ToListAsync())
                .Select(t =>
                {
                    int others = memberships.Count(m => m.UserId != t.RequesterUserId);
                    return new PendingTaskDto
                    {
                        Id = t.Id,
                        RequesterName = names.GetValueOrDefault(t.RequesterUserId, "Invitado"),
                        Description = t.Description,
                        Difficulty = t.Difficulty,
                        XpEarned = t.XpEarned,
                        Votes = t.Approvals.Count,
                        Needed = others / 2 + 1,
                        ViewerVoted = t.Approvals.Contains(viewerId),
                        ViewerIsRequester = t.RequesterUserId == viewerId,
                        HoursLeft = Math.Max(0, (int)Math.Ceiling(
                            (t.CreatedAt.AddHours(FocusCleanupHostedService.ApprovalAutoApproveHours) - DateTime.UtcNow).TotalHours))
                    };
                }).ToList()
        };
    }

    /// <summary>Voto para hacer nacer el huevo del grupo. Nace cuando TODOS los miembros actuales (≥2) votaron.
    /// Devuelve (nació, votos, total miembros).</summary>
    public async Task<(bool hatched, int votes, int members)> VoteToHatchAsync(Guid groupId, Guid userId)
    {
        var group = await _context.Groups.Include(g => g.SharedPet).FirstOrDefaultAsync(g => g.Id == groupId);
        if (group?.SharedPet == null) throw new GroupException(404, "Grupo no encontrado");
        var pet = group.SharedPet;

        var members = await _context.GroupMemberships
            .Where(m => m.GroupId == groupId).Select(m => m.UserId).ToListAsync();
        if (!members.Contains(userId)) throw new GroupException(403, "No eres miembro de este grupo");
        if (pet.IsHatched) return (true, members.Count, members.Count); // ya nació

        if (members.Count < 2) throw new GroupException(409, "Se necesitan al menos 2 miembros para que nazca.");

        // T24: votos de nacimiento concurrentes también se serializan (lost-update en el JSON).
        using var _ = await _lock.AcquireAsync(pet.Id);
        if (!pet.HatchVotes.Contains(userId)) pet.HatchVotes.Add(userId);

        // Unanimidad sobre los miembros ACTUALES (igual que la aprobación de unión).
        int votes = members.Count(m => pet.HatchVotes.Contains(m));
        bool hatched = votes >= members.Count;
        if (hatched)
        {
            pet.IsHatched = true;
            pet.HatchVotes.Clear();
        }
        await _context.SaveChangesAsync();
        return (pet.IsHatched, hatched ? members.Count : votes, members.Count);
    }

    public async Task<List<JoinRequest>> GetPendingRequestsAsync(Guid groupId, Guid viewerId)
    {
        await EnsureMemberAsync(groupId, viewerId);
        return await _context.JoinRequests.Where(j => j.GroupId == groupId).ToListAsync();
    }

    public async Task LeaveGroupAsync(Guid groupId, Guid userId)
    {
        var membership = await _context.GroupMemberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (membership == null) throw new GroupException(404, "No eres miembro");
        _context.GroupMemberships.Remove(membership);

        // Limpia la solicitud propia / saca el voto de las ajenas.
        var reqs = await _context.JoinRequests.Where(j => j.GroupId == groupId).ToListAsync();
        foreach (var r in reqs)
        {
            if (r.RequesterUserId == userId) _context.JoinRequests.Remove(r);
            else r.Approvals.Remove(userId);
        }

        // T24: lo mismo con las tareas pendientes de validación — sin esto el voto del que se fue
        // quedaba contando (voto fantasma: podía completar la mayoría él solo) y sus propias
        // solicitudes quedaban huérfanas para siempre.
        var tas = await _context.TaskApprovals.Where(t => t.GroupId == groupId).ToListAsync();
        foreach (var t in tas)
        {
            if (t.RequesterUserId == userId) _context.TaskApprovals.Remove(t);
            else t.Approvals.Remove(userId);
        }

        var remaining = await _context.GroupMemberships
            .Where(m => m.GroupId == groupId && m.UserId != userId).Select(m => m.UserId).ToListAsync();
        var group = await _context.Groups.Include(g => g.SharedPet).FirstOrDefaultAsync(g => g.Id == groupId);
        if (remaining.Count == 0)
        {
            if (group != null)
            {
                _context.JoinRequests.RemoveRange(_context.JoinRequests.Where(j => j.GroupId == groupId));
                _context.TaskApprovals.RemoveRange(_context.TaskApprovals.Where(t => t.GroupId == groupId));
                _context.Groups.Remove(group);            // dependiente primero (FK → Pets)
                if (group.SharedPet != null) _context.SharedPets.Remove(group.SharedPet);
            }
            await _context.SaveChangesAsync();
            return;
        }

        if (group?.SharedPet != null)
        {
            // No dejar datos huérfanos del que se va.
            group.SharedPet.HatchVotes.Remove(userId);
            group.SharedPet.UserAffection.Remove(userId);

            // T24 — unanimidad PASIVA: al achicarse el grupo, un voto de nacimiento que ya tenían
            // todos los restantes debe surtir efecto ahora (antes quedaba atascado sin gatillo).
            if (!group.SharedPet.IsHatched && remaining.Count >= 2 &&
                remaining.All(m => group.SharedPet.HatchVotes.Contains(m)))
            {
                group.SharedPet.IsHatched = true;
                group.SharedPet.HatchVotes.Clear();
            }
        }
        await _context.SaveChangesAsync();

        // T24 — ídem con solicitudes de unión que quedaron unánimes al irse quien no votaba.
        foreach (var r in reqs.Where(r => r.RequesterUserId != userId).ToList())
        {
            if (remaining.All(m => r.Approvals.Contains(m)))
            {
                try { await CompleteJoinAsync(r); }
                catch (GroupException) { /* grupo lleno: la solicitud ya fue removida */ }
            }
        }
    }
}
