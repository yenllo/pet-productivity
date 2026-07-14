using Microsoft.AspNetCore.SignalR.Client;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.Services;

/// <summary>
/// Conexión SignalR al FamilyHub: presencia/semáforo, Frenesí y estado de mascota en vivo.
/// Singleton; arranca cuando hay usuario. Los eventos se reexponen como C# events para los ViewModels.
/// </summary>
public class RealtimeService
{
    private readonly SettingsService _settings;
    private readonly AuthService _auth;
    private HubConnection? _conn;
    private SyncStatus _myStatus = SyncStatus.Available; // para filtrar "alguien trabaja"

    public event Action<Guid, List<MemberPresence>>? PresenceChanged;
    public event Action<Guid, bool>? FrenzyChanged;
    public event Action<Guid, PetStateDto>? PetUpdated;
    public event Action<Guid, string>? SomeoneWorking;
    public event Action<Guid>? PetHatched;              // el huevo del grupo nació (reveal sincronizado)
    public event Action<Guid, int, int>? HatchProgress; // (groupId, votos, miembros)
    public event Action<Guid>? TaskPending;             // AC4: tarea de grupo esperando validación
    public event Action<Guid>? TaskApproved;            // AC4: tarea validada (recompensa aplicada)
    public event Action<Guid>? GroupFocusStarted;       // F3: alguien inició/se unió a un foco grupal

    public RealtimeService(SettingsService settings, AuthService auth)
    {
        _settings = settings;
        _auth = auth;
    }

    public async Task StartAsync()
    {
        if (_conn != null) return; // idempotente
        if (string.IsNullOrEmpty(_auth.CurrentToken)) return;

        var url = $"{_settings.ServerUrl.TrimEnd('/')}/hubs/family";
        _conn = new HubConnectionBuilder()
            .WithUrl(url, options => options.AccessTokenProvider = () => Task.FromResult<string?>(_auth.CurrentToken))
            .WithAutomaticReconnect().Build();

        _conn.On<Guid, List<MemberPresence>>("Presence", (g, list) => PresenceChanged?.Invoke(g, list));
        _conn.On<Guid, bool>("Frenzy", (g, active) => FrenzyChanged?.Invoke(g, active));
        _conn.On<Guid, PetStateDto>("PetUpdate", (g, pet) => PetUpdated?.Invoke(g, pet));
        _conn.On<Guid>("PetHatched", g => PetHatched?.Invoke(g));
        _conn.On<Guid, int, int>("HatchProgress", (g, v, m) => HatchProgress?.Invoke(g, v, m));
        _conn.On<Guid>("TaskPending", g => TaskPending?.Invoke(g));
        _conn.On<Guid>("TaskApproved", g => TaskApproved?.Invoke(g));
        _conn.On<Guid>("GroupFocusStarted", g => GroupFocusStarted?.Invoke(g));
        _conn.On<Guid>("GroupFocusJoined", g => GroupFocusStarted?.Invoke(g));
        // Solo me interesa el aviso si YO estoy Disponible (así el propio trabajador y los Ocupados no lo ven).
        _conn.On<Guid, string>("SomeoneWorking", (g, name) =>
        {
            if (_myStatus == SyncStatus.Available) SomeoneWorking?.Invoke(g, name);
        });

        // T25: la reconexión automática crea una conexión NUEVA (el server re-une a los grupos en
        // OnConnectedAsync), pero el estado del semáforo se pierde — sin esto, un "Trabajando" volvía
        // como default tras un corte de red y el Frenesí se caía. Re-sincronizar al reconectar.
        _conn.Reconnected += async _ =>
        {
            try
            {
                await _conn.InvokeAsync("RefreshGroups");
                await _conn.InvokeAsync("SetStatus", _myStatus);
            }
            catch (Exception ex) { Console.WriteLine($"Reconnect resync failed: {ex.Message}"); }
        };

        try { await _conn.StartAsync(); }
        catch (Exception ex) { Console.WriteLine($"Realtime start failed: {ex.Message}"); _conn = null; }
    }

    // Re-sincroniza los grupos del usuario en el servidor (familias creadas/unidas tras conectar).
    public async Task RefreshGroupsAsync()
    {
        if (_conn == null) await StartAsync();
        if (_conn?.State == HubConnectionState.Connected)
        {
            try { await _conn.InvokeAsync("RefreshGroups"); }
            catch (Exception ex) { Console.WriteLine($"RefreshGroups failed: {ex.Message}"); }
        }
    }

    public async Task SetStatusAsync(SyncStatus status)
    {
        _myStatus = status;
        if (_conn == null) await StartAsync();
        if (_conn?.State == HubConnectionState.Connected)
        {
            try { await _conn.InvokeAsync("SetStatus", status); }
            catch (Exception ex) { Console.WriteLine($"SetStatus failed: {ex.Message}"); }
        }
    }
}
