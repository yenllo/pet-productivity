using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Client.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.ViewModels;

public partial class HubViewModel : ObservableObject
{
    private readonly GameDataService _game;
    private readonly GroupService _groups;
    private readonly RealtimeService _realtime;
    private readonly PushRegistration _push;

    [ObservableProperty] private ObservableCollection<PetTile> pets = new();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool showEmpty;

    public HubViewModel(GameDataService game, GroupService groups, RealtimeService realtime, PushRegistration push)
    {
        _game = game;
        _groups = groups;
        _realtime = realtime;
        _push = push;
        _realtime.SomeoneWorking += OnSomeoneWorking;
    }

    private void OnSomeoneWorking(Guid groupId, string username)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
            await (Shell.Current?.DisplayAlert(L.T("Tu familia se activó"),
                L.F("{0} empezó a trabajar 🔨 ¡Únete para un Frenesí!", username), "OK") ?? Task.CompletedTask));
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await _game.InitializeAsync();
            await _realtime.StartAsync();      // conecta el tiempo real cuando ya hay usuario
            await _realtime.RefreshGroupsAsync(); // re-sincroniza presencia con las familias actuales
            _ = _push.RegisterAsync();         // registra el token FCM (no bloquea la UI)
            Pets.Clear();

            // Solo familias: la mascota personal vive en su propia pestaña (Mascota).
            foreach (var g in await _groups.GetMyGroupsAsync())
                Pets.Add(PetTile.FromGroup(g));
        }
        finally
        {
            IsBusy = false;
            ShowEmpty = Pets.Count == 0;
        }
    }

    [RelayCommand]
    private async Task OpenPet(PetTile tile)
    {
        if (tile == null) return;
        await Shell.Current.GoToAsync($"PetDetailPage?groupId={tile.GroupId}");
    }

    [RelayCommand]
    private async Task CreateFamily() => await Shell.Current.GoToAsync("CreateGroupPage");

    // T27-L2 (#12): overlay del app para el código (antes DisplayPromptAsync nativo).
    [ObservableProperty] private bool showJoinPrompt;
    [ObservableProperty] private string joinCodeText = string.Empty;

    [RelayCommand]
    private void JoinByCode() { JoinCodeText = string.Empty; ShowJoinPrompt = true; }

    [RelayCommand]
    private async Task ConfirmJoin()
    {
        var code = (JoinCodeText ?? string.Empty).Trim().ToUpperInvariant();
        ShowJoinPrompt = false;
        if (string.IsNullOrWhiteSpace(code)) return;
        var (ok, msg) = await _groups.JoinByCodeAsync(code);
        await Shell.Current.DisplayAlert(ok ? L.T("Listo") : L.T("No se pudo"), msg, "OK");
        if (ok) await InitializeAsync();
    }

    [RelayCommand]
    private void CancelJoinPrompt() => ShowJoinPrompt = false;
}

public class PetTile
{
    public string Name { get; set; } = string.Empty;
    public string ImageSource { get; set; } = "pet_egg.png";
    public string Subtitle { get; set; } = string.Empty;
    public Guid GroupId { get; set; }

    public static PetTile FromGroup(Group g) => new()
    {
        Name = g.Name,
        GroupId = g.Id,
        Subtitle = "Familia",
        ImageSource = PetVisuals.GroupSprite(g.GroupArchetype, EvolutionStage.Adult)
    };
}
