using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Client.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.ViewModels;

[QueryProperty(nameof(GroupId), "groupId")]
public partial class PetDetailViewModel : ObservableObject
{
    private readonly GroupService _groups;
    private readonly RealtimeService _realtime;
    private readonly GameDataService _game;
    private Guid _gid;
    private Guid _petId;
    private bool _subscribed;

    private List<MemberDto> _roster = new();
    private readonly Dictionary<Guid, SyncStatus> _presence = new();
    private Dictionary<Guid, double> _affection = new();

    [ObservableProperty] private string groupId = string.Empty;
    [ObservableProperty] private string petName = string.Empty;
    [ObservableProperty] private string petImageSource = "pet_baby.png";
    [ObservableProperty] private string inviteCode = string.Empty;
    [ObservableProperty] private bool isDormant;
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private bool isFrenzyActive;
    [ObservableProperty] private string healthText = string.Empty;
    // T5: misma burbuja de humor que el Dashboard (PetVisuals.MoodEmoji → humores consistentes).
    [ObservableProperty] private string moodEmoji = string.Empty;
    [ObservableProperty] private ObservableCollection<MemberRow> members = new();
    [ObservableProperty] private ObservableCollection<JoinRequest> pendingRequests = new();
    [ObservableProperty] private bool hasPending;
    // T27: spinner mientras carga el detalle (cold start de Render ~22s) + aviso si la carga falla,
    // en vez de una pantalla en blanco/vieja muda.
    [ObservableProperty] private bool isLoading;

    // AC4: validación social de tareas de grupo
    [ObservableProperty] private ObservableCollection<PendingTaskDto> pendingTasks = new();
    [ObservableProperty] private bool hasPendingTasks;

    // Nacimiento de grupo (huevo → bebé)
    [ObservableProperty] private bool isHatched = true;
    [ObservableProperty] private bool isEgg;
    [ObservableProperty] private bool isHatching;
    [ObservableProperty] private bool showHatchButton;
    [ObservableProperty] private bool showHatchWaiting;
    [ObservableProperty] private bool needsMembers;
    [ObservableProperty] private bool viewerVoted;
    [ObservableProperty] private string hatchInfo = string.Empty;
    private int _memberCount;
    private int _hatchVotes;
    private bool _revealing;

    // F3: foco grupal
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupFocusButtonText))]
    private bool groupFocusActive;
    public string GroupFocusButtonText => GroupFocusActive ? L.T("Únete al foco grupal") : L.T("🎯 Iniciar foco grupal");

    public PetDetailViewModel(GroupService groups, RealtimeService realtime, GameDataService game)
    {
        _groups = groups;
        _realtime = realtime;
        _game = game;
    }

    // El parámetro de navegación llega async; al setearse, inicializamos (evita el race con OnAppearing).
    partial void OnGroupIdChanged(string value) => _ = InitializeAsync();

    public async Task InitializeAsync()
    {
        if (!Guid.TryParse(GroupId, out _gid)) return;

        if (!_subscribed)
        {
            _realtime.PresenceChanged += OnPresence;
            _realtime.FrenzyChanged += OnFrenzy;
            _realtime.PetUpdated += OnPetUpdate;
            _realtime.PetHatched += OnPetHatched;
            _realtime.HatchProgress += OnHatchProgress;
            _realtime.TaskPending += OnTaskEvent;
            _realtime.TaskApproved += OnTaskEvent;
            _realtime.GroupFocusStarted += OnGroupFocus;
            _subscribed = true;
        }
        await _realtime.StartAsync();
        await _realtime.RefreshGroupsAsync(); // une la presencia de esta familia (creada/unida tras conectar)

        IsLoading = Members.Count == 0; // primera carga: muestra spinner (no en refrescos con datos ya en pantalla)
        GroupDetailDto? d;
        try { d = await _groups.GetDetailAsync(_gid); }
        finally { IsLoading = false; }
        if (d == null)
        {
            try { await Toast.Make(L.T("No se pudo cargar la familia. Revisa tu conexión.")).Show(); } catch { }
            return;
        }

        PetName = d.Pet?.Name ?? d.Group.Name;
        _petId = d.Group.SharedPetId;
        PetImageSource = Services.PetVisuals.GroupSprite(d.Group.GroupArchetype, d.Pet?.EvolutionStage ?? EvolutionStage.Baby);
        InviteCode = d.Group.InviteCode;
        IsDormant = d.IsDormant;
        IsHatched = d.IsHatched;
        ViewerVoted = d.ViewerVoted;
        _memberCount = d.MemberCount;
        _hatchVotes = d.HatchVotes;
        UpdateHatchUi();
        _roster = d.Members;
        _affection = d.Pet?.UserAffection ?? new();
        IsFrenzyActive = d.IsFrenzyActive; // snapshot al abrir; los eventos lo refrescan
        _presence.Clear();
        foreach (var m in d.Members) _presence[m.UserId] = m.Status;
        if (d.Pet != null)
        {
            HealthText = $"❤️ {d.Pet.Health:0}/{d.Pet.MaxHealth:0}    ⭐ {d.Pet.TotalXp:0} XP";
            MoodEmoji = Services.PetVisuals.MoodEmoji(d.Pet);
        }

        PendingRequests = new(d.PendingRequests);
        HasPending = PendingRequests.Count > 0;
        PendingTasks = new(d.PendingTasks);
        HasPendingTasks = PendingTasks.Count > 0;
        RebuildMembers();

        var gf = await _game.GetActiveGroupFocusAsync(_gid);
        GroupFocusActive = gf is { Active: true };
    }

    private void OnGroupFocus(Guid g)
    {
        if (g != _gid) return;
        MainThread.BeginInvokeOnMainThread(() => GroupFocusActive = true);
    }

    [RelayCommand]
    private async Task GroupFocus()
    {
        if (!IsHatched || IsDormant)
        {
            await Toast.Make(L.T("La mascota debe estar despierta (mín. 2 miembros).")).Show();
            return;
        }
        var img = Uri.EscapeDataString(PetImageSource ?? string.Empty);
        if (GroupFocusActive)
        {
            await Shell.Current.GoToAsync($"FocusPage?joinGroupId={_gid}&petImage={img}");
            return;
        }
        string topic = await Shell.Current.DisplayPromptAsync(L.T("Foco grupal"), L.T("¿En qué van a trabajar juntos?"),
            "Empezar", "Cancelar", "Estudiar juntos");
        if (string.IsNullOrWhiteSpace(topic)) return;
        await Shell.Current.GoToAsync($"FocusPage?groupId={_gid}&description={Uri.EscapeDataString(topic)}&petImage={img}");
    }

    [RelayCommand]
    private async Task GroupHistory() => await Shell.Current.GoToAsync($"HistoryPage?groupId={_gid}");

    private void OnTaskEvent(Guid g)
    {
        if (g != _gid) return;
        MainThread.BeginInvokeOnMainThread(async () => await InitializeAsync());
    }

    [RelayCommand]
    private async Task ApproveTask(PendingTaskDto t)
    {
        if (t == null) return;
        var (ok, msg) = await _groups.ApproveTaskAsync(t.Id);
        if (!ok) await Toast.Make(msg).Show();
        await InitializeAsync();
    }

    public void Cleanup()
    {
        _realtime.PresenceChanged -= OnPresence;
        _realtime.FrenzyChanged -= OnFrenzy;
        _realtime.PetUpdated -= OnPetUpdate;
        _realtime.PetHatched -= OnPetHatched;
        _realtime.HatchProgress -= OnHatchProgress;
        _realtime.TaskPending -= OnTaskEvent;
        _realtime.TaskApproved -= OnTaskEvent;
        _realtime.GroupFocusStarted -= OnGroupFocus;
        _subscribed = false;
    }

    // ---------- Nacimiento de grupo (huevo) ----------
    private void UpdateHatchUi()
    {
        IsEgg = !IsHatched && !IsHatching;
        NeedsMembers = !IsHatched && _memberCount < 2;
        bool canHatch = !IsHatched && _memberCount >= 2 && !IsHatching;
        ShowHatchButton = canHatch && !ViewerVoted;
        ShowHatchWaiting = canHatch && ViewerVoted;
        HatchInfo = $"{_hatchVotes}/{_memberCount} listos para nacer";
        StatusText = IsHatched
            ? (IsDormant ? L.T("Dormida — comparte el código (mín. 2 miembros)") : L.T("Activa"))
            : (NeedsMembers ? L.T("Huevo — comparte el código (mín. 2 miembros)")
                            : L.T("Huevo — todos deben tocar «Hacer nacer»"));
    }

    private void OnHatchProgress(Guid g, int votes, int members)
    {
        if (g != _gid) return;
        _hatchVotes = votes; _memberCount = members;
        MainThread.BeginInvokeOnMainThread(UpdateHatchUi);
    }

    private void OnPetHatched(Guid g)
    {
        if (g != _gid) return;
        MainThread.BeginInvokeOnMainThread(async () => await PlayRevealAsync());
    }

    private async Task PlayRevealAsync()
    {
        if (_revealing || IsHatched) return;
        _revealing = true;
        IsHatching = true; UpdateHatchUi();   // muestra egg_crack.gif
        await Task.Delay(2200);
        IsHatching = false; IsHatched = true;  // revela al bebé
        UpdateHatchUi();
        _revealing = false;
    }

    [RelayCommand]
    private async Task Hatch()
    {
        var (hatched, votes, members, msg) = await _groups.HatchAsync(_gid);
        if (!string.IsNullOrEmpty(msg)) { await Toast.Make(msg).Show(); return; }
        ViewerVoted = true;
        _hatchVotes = votes; _memberCount = members;
        UpdateHatchUi();
        if (hatched) await PlayRevealAsync(); // por si el broadcast no me llega
    }

    private void OnPresence(Guid g, List<MemberPresence> list)
    {
        if (g != _gid) return;
        _presence.Clear();
        foreach (var p in list) _presence[p.UserId] = p.Status;
        MainThread.BeginInvokeOnMainThread(RebuildMembers);
    }

    private void OnFrenzy(Guid g, bool active)
    {
        if (g != _gid) return;
        MainThread.BeginInvokeOnMainThread(() => IsFrenzyActive = active);
    }

    private void OnPetUpdate(Guid g, PetStateDto pet)
    {
        if (g != _gid) return;
        _affection = pet.Affection ?? new();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            HealthText = $"❤️ {pet.Health:0}/{pet.MaxHealth:0}    ⭐ {pet.TotalXp:0} XP";
            // El DTO en vivo no trae Hunger, pero las compartidas no decaen (hambre ≈ 100):
            // mismos umbrales que Pet.Condition con hambre llena.
            MoodEmoji = pet.Health < Pet.WeakAt ? "💔" : pet.Health > 70 ? "✨" : "";
            RebuildMembers();
        });
    }

    private void RebuildMembers()
    {
        Members.Clear();
        foreach (var m in _roster)
        {
            var status = _presence.TryGetValue(m.UserId, out var s) ? s : SyncStatus.Offline;
            var aff = _affection.TryGetValue(m.UserId, out var a) ? a : m.Affection;
            Members.Add(new MemberRow
            {
                Username = m.Username,
                StatusColor = ColorFor(status),
                StatusText = StatusLabel(status),
                Mood = MoodFor(aff),
                Affection = aff
            });
        }
    }

    private static Color ColorFor(SyncStatus s) => s switch
    {
        SyncStatus.Available => Color.FromArgb("#4CAF50"),
        SyncStatus.Working => Color.FromArgb("#FF9800"),
        SyncStatus.Busy => Color.FromArgb("#E57373"),
        _ => Color.FromArgb("#9E9E9E")
    };
    private static string StatusLabel(SyncStatus s) => s switch
    {
        SyncStatus.Available => "Disponible",
        SyncStatus.Working => "Trabajando",
        SyncStatus.Busy => "Ocupado",
        _ => "Desconectado"
    };
    private static string MoodFor(double aff) => aff > 70 ? L.T("Feliz") : aff > 30 ? "Neutral" : L.T("Huraño");

    [RelayCommand]
    private async Task RegisterTask()
    {
        if (!IsHatched)
        {
            await Toast.Make(L.T("El huevo necesita que todos toquen «Hacer nacer».")).Show();
            return;
        }
        if (IsDormant)
        {
            await Toast.Make(L.T("La mascota necesita al menos 2 miembros para recibir tareas.")).Show();
            return;
        }
        await Shell.Current.GoToAsync($"TaskPage?petId={_petId}&petName={Uri.EscapeDataString(PetName)}&petImage={Uri.EscapeDataString(PetImageSource ?? string.Empty)}");
    }

    [RelayCommand]
    private async Task Approve(JoinRequest req)
    {
        if (req == null) return;
        var (ok, msg) = await _groups.ApproveAsync(req.Id);
        await Toast.Make(msg).Show();
        await InitializeAsync();
    }

    [RelayCommand]
    private async Task Leave()
    {
        if (!Guid.TryParse(GroupId, out var gid)) return;
        bool yes = await Shell.Current.DisplayAlert(L.T("Salir"), L.F("¿Salir de {0}?", PetName), L.T("Salir"), L.T("Cancelar"));
        if (!yes) return;
        await _groups.LeaveAsync(gid);
        await Shell.Current.GoToAsync("..");
    }
}

public class MemberRow
{
    public string Username { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public string StatusText { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public double Affection { get; set; }
}
