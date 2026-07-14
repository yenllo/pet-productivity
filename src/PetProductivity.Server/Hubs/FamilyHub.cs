using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PetProductivity.Server.Data;
using PetProductivity.Server.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Hubs;

/// <summary>
/// Tiempo real por familia: semáforo (presencia/estado), Frenesí y avisos.
/// Identificación por JWT (el token va por query `?access_token=` porque los WebSockets no llevan headers).
/// </summary>
[Authorize]
public class FamilyHub : Hub
{
    private readonly AppDbContext _db;
    private readonly PresenceService _presence;
    private readonly PushService _push;

    public FamilyHub(AppDbContext db, PresenceService presence, PushService push)
    {
        _db = db;
        _presence = presence;
        _push = push;
    }

    private Guid UserId => Context.User?.GetUserId() ?? Guid.Empty;

    public override async Task OnConnectedAsync()
    {
        var userId = UserId;
        if (userId != Guid.Empty)
        {
            var user = await _db.Users.FindAsync(userId);
            // Conectarse = al menos Disponible (no arrastrar un Offline persistido).
            var status = (user == null || user.CurrentStatus == SyncStatus.Offline)
                ? SyncStatus.Available : user.CurrentStatus;

            var groupIds = await _db.GroupMemberships
                .Where(m => m.UserId == userId).Select(m => m.GroupId).ToListAsync();

            // Frenesí antes de unirse, para detectar si esta conexión (ya Trabajando) lo enciende.
            var beforeFrenzy = groupIds.ToDictionary(g => g, g => _presence.IsFrenzyActive(g));

            _presence.Join(Context.ConnectionId, userId, groupIds, status);
            foreach (var gid in groupIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, Room(gid));

            await BroadcastGroups(groupIds);

            // Si conectarse encendió el Frenesí (off→on), avisar por push a los offline.
            foreach (var gid in groupIds)
                if (!beforeFrenzy[gid] && _presence.IsFrenzyActive(gid))
                    await PushFrenzyAsync(gid);
        }
        await base.OnConnectedAsync();
    }

    // Re-sincroniza los grupos del usuario en esta conexión (para familias creadas/unidas
    // DESPUÉS de conectar, que no quedaron en la presencia/grupos al conectar).
    public async Task RefreshGroups()
    {
        var userId = UserId;
        if (userId == Guid.Empty) return;

        var user = await _db.Users.FindAsync(userId);
        var status = (user == null || user.CurrentStatus == SyncStatus.Offline)
            ? SyncStatus.Available : user.CurrentStatus;

        var groupIds = await _db.GroupMemberships
            .Where(m => m.UserId == userId).Select(m => m.GroupId).ToListAsync();

        _presence.Join(Context.ConnectionId, userId, groupIds, status);
        foreach (var gid in groupIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, Room(gid));

        await BroadcastGroups(groupIds);
    }

    public async Task SetStatus(SyncStatus status)
    {
        var userId = UserId;
        if (userId == Guid.Empty) return;

        var user = await _db.Users.FindAsync(userId);
        if (user != null) { user.CurrentStatus = status; await _db.SaveChangesAsync(); }

        var (affected, frenzyStarted) = _presence.SetStatusWithFrenzy(userId, status);
        await BroadcastGroups(affected);

        if (status == SyncStatus.Working)
        {
            var name = user?.Username ?? "Alguien";
            // Difunde a toda la familia; el cliente muestra el aviso solo si ÉL está Available
            // (así el propio trabajador y los Ocupados no lo ven).
            foreach (var gid in affected)
                await Clients.Group(Room(gid)).SendAsync("SomeoneWorking", gid, name);
        }

        // Push (app cerrada) a los miembros NO conectados cuando arranca un Frenesí.
        foreach (var gid in frenzyStarted)
            await PushFrenzyAsync(gid);
    }

    private async Task PushFrenzyAsync(Guid groupId)
    {
        var group = await _db.Groups.FindAsync(groupId);
        var connected = _presence.GetPresence(groupId).Select(p => p.UserId).ToList();
        var offline = await _db.GroupMemberships
            .Where(m => m.GroupId == groupId && !connected.Contains(m.UserId))
            .Select(m => m.UserId).ToListAsync();
        if (offline.Count > 0)
            await _push.SendToUsersAsync(offline, "🔥 ¡Frenesí!",
                $"Tu familia {group?.Name} está trabajando. ¡Únete y gana x2 XP!");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var affected = _presence.Disconnect(Context.ConnectionId);
        await BroadcastGroups(affected);
        await base.OnDisconnectedAsync(exception);
    }

    public static string Room(Guid groupId) => $"family-{groupId}";

    private async Task BroadcastGroups(List<Guid> groupIds)
    {
        foreach (var gid in groupIds)
        {
            var presence = _presence.GetPresence(gid);
            var ids = presence.Select(p => p.UserId).ToList();
            var names = await _db.Users.Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            var payload = presence.Select(p => new
            {
                userId = p.UserId,
                username = names.GetValueOrDefault(p.UserId, "?"),
                status = p.Status
            });

            await Clients.Group(Room(gid)).SendAsync("Presence", gid, payload);
            await Clients.Group(Room(gid)).SendAsync("Frenzy", gid, _presence.IsFrenzyActive(gid));
        }
    }
}
