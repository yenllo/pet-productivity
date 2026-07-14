using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

/// <summary>
/// Presencia y Frenesí por familia, en memoria (transitorio, no persistido).
/// El Frenesí es DERIVADO: activo cuando ≥2 miembros presentes de la familia están "Working".
/// </summary>
public class PresenceService
{
    private readonly object _lock = new();
    // groupId -> (userId -> status) — solo miembros CONECTADOS.
    private readonly Dictionary<Guid, Dictionary<Guid, SyncStatus>> _groups = new();
    // connectionId -> (userId, groupIds) — para limpiar al desconectar.
    private readonly Dictionary<string, (Guid userId, List<Guid> groupIds)> _conns = new();

    public void Join(string connId, Guid userId, List<Guid> groupIds, SyncStatus status)
    {
        lock (_lock)
        {
            _conns[connId] = (userId, groupIds);
            foreach (var gid in groupIds)
            {
                if (!_groups.TryGetValue(gid, out var members))
                    _groups[gid] = members = new();
                members[userId] = status;
            }
        }
    }

    /// <summary>Cambia el estado del usuario en todas sus familias. Devuelve los grupos afectados.</summary>
    public List<Guid> SetStatus(Guid userId, SyncStatus status)
    {
        lock (_lock)
        {
            var affected = new List<Guid>();
            foreach (var (gid, members) in _groups)
            {
                if (members.ContainsKey(userId))
                {
                    members[userId] = status;
                    affected.Add(gid);
                }
            }
            return affected;
        }
    }

    /// <summary>Como SetStatus, pero además devuelve los grupos que ACABAN de entrar en Frenesí (off→on), para push.</summary>
    public (List<Guid> Affected, List<Guid> FrenzyStarted) SetStatusWithFrenzy(Guid userId, SyncStatus status)
    {
        lock (_lock)
        {
            var affected = new List<Guid>();
            var frenzyStarted = new List<Guid>();
            foreach (var (gid, members) in _groups)
            {
                if (members.ContainsKey(userId))
                {
                    bool was = members.Count(kv => kv.Value == SyncStatus.Working) >= 2;
                    members[userId] = status;
                    bool now = members.Count(kv => kv.Value == SyncStatus.Working) >= 2;
                    affected.Add(gid);
                    if (now && !was) frenzyStarted.Add(gid);
                }
            }
            return (affected, frenzyStarted);
        }
    }

    public List<Guid> Disconnect(string connId)
    {
        lock (_lock)
        {
            if (!_conns.TryGetValue(connId, out var info)) return new();
            _conns.Remove(connId);
            bool stillPresent = _conns.Values.Any(v => v.userId == info.userId); // otra conexión viva
            var affected = new List<Guid>();
            foreach (var gid in info.groupIds)
            {
                if (_groups.TryGetValue(gid, out var members))
                {
                    if (!stillPresent) members.Remove(info.userId);
                    affected.Add(gid);
                }
            }
            return affected;
        }
    }

    public List<(Guid UserId, SyncStatus Status)> GetPresence(Guid groupId)
    {
        lock (_lock)
        {
            return _groups.TryGetValue(groupId, out var members)
                ? members.Select(kv => (kv.Key, kv.Value)).ToList()
                : new();
        }
    }

    public bool IsFrenzyActive(Guid groupId)
    {
        lock (_lock)
        {
            return _groups.TryGetValue(groupId, out var members)
                && members.Count(kv => kv.Value == SyncStatus.Working) >= 2;
        }
    }
}
