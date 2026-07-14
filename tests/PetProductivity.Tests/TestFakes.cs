using Microsoft.AspNetCore.SignalR;
using PetProductivity.Server.Hubs;
using PetProductivity.Server.Services;

namespace PetProductivity.Tests;

// Fakes mínimos compartidos por los tests que construyen PetService (ApproveTaskTests, RitualTests…).
internal class SilentAi : IAiService
{
    public Task<string> GenerateContentAsync(string prompt) => Task.FromResult("");
    public Task<string> GenerateFromImageAsync(string prompt, byte[] imageBytes, string mimeType) => Task.FromResult("");
}

internal class FakeHubContext : IHubContext<FamilyHub>
{
    public IHubClients Clients { get; } = new FakeClients();
    public IGroupManager Groups { get; } = new FakeGroups();
}

internal class FakeClients : IHubClients
{
    private static readonly IClientProxy P = new FakeProxy();
    public IClientProxy All => P;
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => P;
    public IClientProxy Client(string connectionId) => P;
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => P;
    public IClientProxy Group(string groupName) => P;
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => P;
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => P;
    public IClientProxy User(string userId) => P;
    public IClientProxy Users(IReadOnlyList<string> userIds) => P;
}

internal class FakeProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal class FakeGroups : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
