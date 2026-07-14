using System.Net;
using System.Net.Http.Json;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.Services;

public class GroupService
{
    private readonly HttpClient _http;
    private readonly SettingsService _settings;
    private readonly AuthService _auth;

    public GroupService(HttpClient http, SettingsService settings, AuthService auth)
    {
        _http = http;
        _settings = settings;
        _auth = auth;
    }

    private string Base => _settings.ServerUrl.TrimEnd('/');
    private Guid Uid => _auth.CurrentUser?.Id ?? Guid.Empty;

    public async Task<List<Group>> GetMyGroupsAsync()
    {
        try
        {
            var r = await _http.GetAsync($"{Base}/api/groups/mine/{Uid}");
            if (r.IsSuccessStatusCode)
                return await r.Content.ReadFromJsonAsync<List<Group>>() ?? new();
        }
        catch (Exception ex) { Console.WriteLine($"GetMyGroups: {ex.Message}"); }
        return new();
    }

    public async Task<GroupDetailDto?> GetDetailAsync(Guid groupId)
    {
        try
        {
            var r = await _http.GetAsync($"{Base}/api/groups/{groupId}");
            if (r.IsSuccessStatusCode)
                return await r.Content.ReadFromJsonAsync<GroupDetailDto>();
        }
        catch (Exception ex) { Console.WriteLine($"GetDetail: {ex.Message}"); }
        return null;
    }

    public async Task<(bool ok, string msg)> CreateGroupAsync(string name, Archetype archetype, int maxMembers)
    {
        try
        {
            var r = await _http.PostAsJsonAsync($"{Base}/api/groups",
                new { UserId = Uid, Name = name, Archetype = archetype, MaxMembers = maxMembers });
            return r.IsSuccessStatusCode ? (true, L.T("¡Familia creada!")) : (false, await r.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> JoinByCodeAsync(string inviteCode)
    {
        try
        {
            var r = await _http.PostAsJsonAsync($"{Base}/api/groups/join",
                new { UserId = Uid, InviteCode = inviteCode });
            return r.IsSuccessStatusCode
                ? (true, "Solicitud enviada. Debe aprobarla cada miembro actual.")
                : (false, await r.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool ok, string msg)> ApproveAsync(Guid requestId)
    {
        try
        {
            var r = await _http.PostAsJsonAsync($"{Base}/api/groups/requests/{requestId}/approve",
                new { UserId = Uid });
            if (!r.IsSuccessStatusCode) return (false, await r.Content.ReadAsStringAsync());
            return (true, r.StatusCode == HttpStatusCode.Accepted
                ? "Aprobado. Faltan otros miembros por aprobar."
                : L.T("¡Nuevo miembro aceptado!"));
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<(bool hatched, int votes, int members, string msg)> HatchAsync(Guid groupId)
    {
        try
        {
            var r = await _http.PostAsync($"{Base}/api/groups/{groupId}/hatch", null);
            if (!r.IsSuccessStatusCode) return (false, 0, 0, await r.Content.ReadAsStringAsync());
            var dto = await r.Content.ReadFromJsonAsync<HatchResult>();
            return (dto?.Hatched ?? false, dto?.Votes ?? 0, dto?.Members ?? 0, "");
        }
        catch (Exception ex) { return (false, 0, 0, ex.Message); }
    }

    public async Task<(bool ok, string msg)> ApproveTaskAsync(Guid approvalId)
    {
        try
        {
            var r = await _http.PostAsync($"{Base}/api/groups/tasks/{approvalId}/approve", null);
            return r.IsSuccessStatusCode ? (true, "") : (false, await r.Content.ReadAsStringAsync());
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    public async Task<bool> LeaveAsync(Guid groupId)
    {
        try
        {
            var r = await _http.DeleteAsync($"{Base}/api/groups/{groupId}/members/{Uid}");
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public class HatchResult
    {
        public bool Hatched { get; set; }
        public int Votes { get; set; }
        public int Members { get; set; }
    }
}
