using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Shared.Models;
using System.Net.Http.Json;
using PetProductivity.Client.Services;

namespace PetProductivity.Client.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    // Estado de la Mascota
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PetImageSource))]
    [NotifyPropertyChangedFor(nameof(BabySprite))]
    [NotifyPropertyChangedFor(nameof(AdultSprite))]
    [NotifyPropertyChangedFor(nameof(MasterSprite))]
    [NotifyPropertyChangedFor(nameof(LevelLabel))]
    [NotifyPropertyChangedFor(nameof(PetSubtitle))]
    [NotifyPropertyChangedFor(nameof(CuerpoXp))]
    [NotifyPropertyChangedFor(nameof(MenteXp))]
    [NotifyPropertyChangedFor(nameof(HogarXp))]
    [NotifyPropertyChangedFor(nameof(BienestarXp))]
    [NotifyPropertyChangedFor(nameof(MoodEmoji))]
    [NotifyPropertyChangedFor(nameof(ShowMood))]
    [NotifyPropertyChangedFor(nameof(HasPet))]
    private Pet currentPet;

    // T27-L2: la sección del nombre se colapsa hasta que haya mascota (evita el hueco vacío al cargar).
    public bool HasPet => CurrentPet != null;

    // T5: la cara del estado real (Pet.Condition, derivado de Hunger/Health). Sin burbuja en Normal.
    public string MoodEmoji => PetVisuals.MoodEmoji(CurrentPet);
    public bool ShowMood => MoodEmoji.Length > 0 && !IsCrystallized;

    // T5-D: reacción visible al ganar XP — la página anima un saltito cuando TotalXp subió
    // desde la última vez que se vio la mascota (estático: sobrevive a VMs transient).
    public event Action? CelebrateXp;
    private static double _lastSeenXp = -1;

    // T4-E: ceremonia de evolución (hoy Baby→Adult→Master ocurre en silencio). La etapa ya celebrada
    // se persiste en Preferences → sobrevive a que maten la app en medio (criterio 1): si al reabrir la
    // etapa real (verdad del server) sigue siendo mayor que la última celebrada, se vuelve a disparar.
    [ObservableProperty] private bool showEvolution;
    [ObservableProperty] private string evolutionText = string.Empty;
    private const string LastCelebratedStageKey = "LastCelebratedStage";

    // T1: racha diaria en la cabecera + "aún nada hoy" (punto naranja).
    [ObservableProperty] private string streakLabel = "🔥 0";
    [ObservableProperty] private bool streakAtRisk;

    // T27-L2 (#18): el chip 🔥 no se entendía y el punto naranja parecía notificación → tap = explicación.
    [RelayCommand]
    private async Task ExplainStreak()
    {
        var n = _gameDataService.CurrentUser?.CurrentStreak ?? 0;
        var msg = StreakAtRisk
            ? L.F("Racha diaria: {0} día(s) seguidos haciendo algo. El punto naranja avisa que HOY aún no registras nada — ¡haz una tarea o un foco para mantenerla!", n)
            : L.F("Racha diaria: {0} día(s) seguidos haciendo algo. ¡Hoy ya está asegurada! ✔", n);
        try { await Toast.Make(msg, CommunityToolkit.Maui.Core.ToastDuration.Long).Show(); } catch { }
    }

    // Sprites de cada etapa de la especie actual (previsualización de evolución).
    public string BabySprite => PetVisuals.SpriteFor(CurrentPet?.Species ?? PetSpecies.Sprout, EvolutionStage.Baby);
    public string AdultSprite => PetVisuals.SpriteFor(CurrentPet?.Species ?? PetSpecies.Sprout, EvolutionStage.Adult);
    public string MasterSprite => PetVisuals.SpriteFor(CurrentPet?.Species ?? PetSpecies.Sprout, EvolutionStage.Master);

    // Datos reales para la cabecera y los anillos (antes placeholders).
    public string LevelLabel => CurrentPet == null ? "Nv 1" : $"Nv {(int)(CurrentPet.TotalXp / 1000) + 1}";
    public string PetSubtitle => CurrentPet == null ? "" : $"{SpeciesName(CurrentPet.Species)} · {StageName(CurrentPet.EvolutionStage)}";
    public string CuerpoXp => $"{CurrentPet?.GetStatValue("Cuerpo") ?? 0:0}";
    public string MenteXp => $"{CurrentPet?.GetStatValue("Mente") ?? 0:0}";
    public string HogarXp => $"{CurrentPet?.GetStatValue("Hogar") ?? 0:0}";
    public string BienestarXp => $"{CurrentPet?.GetStatValue("Bienestar") ?? 0:0}";

    private static string SpeciesName(PetSpecies s) => s switch
    {
        PetSpecies.Sprout => "Brote",
        PetSpecies.Ember => "Ember",
        PetSpecies.Aqua => "Aqua",
        _ => s.ToString()
    };
    private static string StageName(EvolutionStage st) => st switch
    {
        EvolutionStage.Egg => L.T("Huevo"),
        EvolutionStage.Baby => L.T("Cría"),
        EvolutionStage.Adult => L.T("Adulto"),
        EvolutionStage.Master => L.T("Maestro"),
        _ => ""
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrystallized))]
    [NotifyPropertyChangedFor(nameof(PetOpacity))]
    [NotifyPropertyChangedFor(nameof(PetImageSource))]
    [NotifyPropertyChangedFor(nameof(ShowMood))]
    private PetStatus petStatus;

    public bool IsCrystallized => PetStatus == PetStatus.Crystallized;
    public double PetOpacity => IsCrystallized ? 0.5 : 1.0;
    
    // Sprite por especie + etapa (starters Moko). Huevo/cristal tienen su propio sprite.
    public string PetImageSource => PetVisuals.SpriteFor(CurrentPet);

    // Comandos (Botones)
    [RelayCommand]
    public async Task NavigateToNewTask()
    {
        // Tarea para la mascota personal (sin petId). TaskPage ahora es ruta push, no pestaña.
        await Shell.Current.GoToAsync("TaskPage");
    }

    // T27-L1 (#2): banner "volver al foco". Sin esto no había forma de regresar a un foco activo
    // mid-sesión (quedabas fuera y la app pedía "escribe algo antes de iniciar").
    private readonly FocusSessionService _focus;
    [ObservableProperty] private bool isFocusActive;

    [RelayCommand]
    private async Task ResumeFocus() => await Shell.Current.GoToAsync("FocusPage");

    public DashboardViewModel(HttpClient httpClient, GameDataService gameDataService, RealtimeService realtime, SettingsService settings, FocusSessionService focus)
    {
        _httpClient = httpClient;
        _gameDataService = gameDataService;
        _realtime = realtime;
        _settings = settings;
        _focus = focus;

        // Initialize Status
        CurrentStatus = SyncStatus.Offline;

        InitializeRitualGrid();
    }

    [ObservableProperty]
    private bool isGuest;

    // Estilo de habitación equipado (cosmético). Lo lee el RoomDiorama del Dashboard.
    [ObservableProperty]
    private string roomStyle = "default";

    // Muebles poseídos (claves CSV) que el RoomDiorama dibuja en sus slots fijos.
    [ObservableProperty]
    private string furniture = string.Empty;

    // Nombre del ítem de tienda → clave de mueble que dibuja el diorama (poseerlo = colocarlo).
    // (Legado del seed procedural; hoy los muebles reales van por PlacedFurniture. Se conserva por compat.)
    private static readonly Dictionary<string, string> FurnitureKeys = new()
    {
        ["Lámpara de Pie"] = "lamp",
        ["Cuadro Decorativo"] = "poster",
        ["Reloj de Pared"] = "clock",
    };

    // ---- F5.2 colocación de muebles (tipo Sims) ----
    // Si hay colocaciones, el RoomDiorama las dibuja en vez del seed fijo.
    [ObservableProperty]
    private IReadOnlyList<PlacedFurniture>? placements;

    // Sandbox de edición (overlay pantalla completa). EditMode = overlay abierto.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionActionsVisible))]
    [NotifyPropertyChangedFor(nameof(PadVisible))]
    [NotifyPropertyChangedFor(nameof(SandboxHintVisible))]
    private bool editMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionActionsVisible))]
    [NotifyPropertyChangedFor(nameof(PadVisible))]
    [NotifyPropertyChangedFor(nameof(SandboxHintVisible))]
    private bool hasSelection;

    // Modo mover (pad tipo Gameboy sobre el objeto seleccionado).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionActionsVisible))]
    [NotifyPropertyChangedFor(nameof(PadVisible))]
    [NotifyPropertyChangedFor(nameof(SandboxHintVisible))]
    private bool moveMode;

    public bool SelectionActionsVisible => EditMode && HasSelection && !MoveMode;
    public bool PadVisible => EditMode && MoveMode;
    public bool SandboxHintVisible => EditMode && !HasSelection && !MoveMode;

    // Celda origen del mueble seleccionado (para resaltar en el diorama). null = ninguno.
    public (int X, int Y)? SelectedCell { get; private set; }

    private List<PlacedFurniture> _editList = new();
    private int _selectedIndex = -1;

    // ---- Guardados: poseído (Inventory) pero no colocado. Derivado en cliente, sin server. ----
    public ObservableCollection<StoredItemVm> StoredItems { get; } = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StoredHeader))]
    [NotifyPropertyChangedFor(nameof(StoredEmpty))]
    [NotifyPropertyChangedFor(nameof(StoredNotEmpty))]
    private int storedCount;
    public string StoredHeader => L.F("🗄️ Guardados ({0})", StoredCount);
    public bool StoredEmpty => StoredCount == 0;
    public bool StoredNotEmpty => StoredCount > 0;

    // Muebles del cuarto inicial: son gratis y no viven en Inventory, pero deben poder re-colocarse.
    private static readonly (string Name, string Sprite)[] Freebies =
    {
        ("Cama Moderna", "obj_bed_l"), ("Planta", "obj_plant"), ("Gato", "obj_cat_l"),
    };

    private List<ShopItem>? _catalogCache; // para resolver Name (Inventory) → SpriteId

    // Lista "En el cuarto" (espejo de _editList para la tira del sandbox).
    public ObservableCollection<StoredItemVm> PlacedItems { get; } = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlacedHeader))]
    private int placedCount;
    public string PlacedHeader => L.F("🛋️ En el cuarto ({0})", PlacedCount);

    private void RefreshStored()
    {
        StoredItems.Clear();
        var placedNames = _editList.Select(p => p.Name).ToHashSet();
        foreach (var (name, sprite) in Freebies)
            if (!placedNames.Contains(name))
                StoredItems.Add(new StoredItemVm(name, sprite));
        var inv = _gameDataService.CurrentUser?.Inventory;
        if (inv != null && _catalogCache != null)
            foreach (var item in _catalogCache.Where(i => !string.IsNullOrEmpty(i.SpriteId)
                                                          && inv.ContainsKey(i.Name) && !placedNames.Contains(i.Name)))
                StoredItems.Add(new StoredItemVm(item.Name, item.SpriteId));
        StoredCount = StoredItems.Count;

        PlacedItems.Clear();
        foreach (var p in _editList)
            PlacedItems.Add(new StoredItemVm(p.Name, p.Sprite, p));
        PlacedCount = PlacedItems.Count;
    }

    static PlacedFurniture Clone(PlacedFurniture p) =>
        new() { Name = p.Name, Sprite = p.Sprite, GridX = p.GridX, GridY = p.GridY, GridW = p.GridW, GridD = p.GridD };

    // Lápiz ✏️ → abre el sandbox. Se edita sobre CLONES: los objetos canónicos del usuario no se
    // mutan, así "Cancelar" es simplemente descartar la lista de trabajo.
    [RelayCommand]
    private async Task EnterSandbox()
    {
        var cur = _gameDataService.GetPlacements();
        _editList = cur is { Count: > 0 } ? cur.Select(Clone).ToList() : _gameDataService.SeedPlacements();
        Deselect();
        MoveMode = false;
        Placements = new List<PlacedFurniture>(_editList);
        EditMode = true;
        _catalogCache ??= await _gameDataService.GetCatalogAsync();
        RefreshStored();
    }

    [RelayCommand]
    private async Task SaveSandbox()
    {
        await _gameDataService.SavePlacementsAsync(_editList);
        CloseSandbox();
        await Toast.Make(L.T("Cuarto guardado ✓")).Show();
    }

    [RelayCommand]
    private void CancelSandbox()
    {
        CloseSandbox();
        // Vuelve a la verdad no tocada (los clones se descartan).
        Placements = _gameDataService.GetPlacements() is { Count: > 0 } pf ? pf : null;
    }

    private void CloseSandbox()
    {
        Deselect();
        MoveMode = false;
        EditMode = false;
    }

    // Tap en la tira "En el cuarto" → seleccionar ese objeto en el diorama.
    [RelayCommand]
    private void SelectPlaced(StoredItemVm item)
    {
        if (item?.Placement == null) return;
        int i = _editList.IndexOf(item.Placement);
        if (i >= 0) { MoveMode = false; Select(i); }
    }

    // ---- Modo mover: pad tipo Gameboy ----
    private (int X, int Y, string Sprite) _moveBackup;

    [RelayCommand]
    private void StartMove()
    {
        if (_selectedIndex < 0) return;
        var p = _editList[_selectedIndex];
        _moveBackup = (p.GridX, p.GridY, p.Sprite);
        MoveMode = true;
    }

    // Flechas del pad: 1 celda por los ejes de la grilla iso (en pantalla son diagonales).
    [RelayCommand]
    private void MoveStep(string dir)
    {
        if (_selectedIndex < 0) return;
        var p = _editList[_selectedIndex];
        var (dx, dy) = dir switch { "up" => (0, -1), "down" => (0, 1), "left" => (-1, 0), _ => (1, 0) };
        MoveSelected(p.GridX + dx, p.GridY + dy);
    }

    // B del pad: guardar en inventario el objeto que se está moviendo.
    [RelayCommand]
    private void StoreMove()
    {
        MoveMode = false;
        RemoveSelected();
    }

    // ✕ del pad: revierte posición y vista al estado de antes de "Mover".
    [RelayCommand]
    private void CancelMove()
    {
        if (_selectedIndex >= 0)
        {
            var p = _editList[_selectedIndex];
            (p.GridX, p.GridY, p.Sprite) = _moveBackup;
            SelectedCell = (p.GridX, p.GridY);
            OnPropertyChanged(nameof(SelectedCell));
            Placements = new List<PlacedFurniture>(_editList);
        }
        MoveMode = false;
    }

    [RelayCommand]
    private void ConfirmMove() => MoveMode = false;

    // Tap en un guardado → a la primera celda libre, y queda seleccionado para moverlo.
    [RelayCommand]
    private async Task PlaceStored(StoredItemVm item)
    {
        if (!EditMode || item == null) return;
        var (w, d) = GameDataService.FootprintFor(item.Sprite);
        var cell = GameDataService.FindFreeCell(_editList, w, d);
        if (cell == null) { await Toast.Make(L.T("No hay espacio: mueve o quita algo primero.")).Show(); return; }
        _editList.Add(new PlacedFurniture { Name = item.Name, Sprite = item.Sprite, GridX = cell.Value.x, GridY = cell.Value.y, GridW = w, GridD = d });
        Placements = new List<PlacedFurniture>(_editList);
        Select(_editList.Count - 1);
        RefreshStored();
    }

    // La página bridgea el toque del diorama (celda de piso) a esto.
    public void OnCellTapped(int gx, int gy)
    {
        if (!EditMode) return;
        int hit = _editList.FindIndex(p => gx >= p.GridX && gx < p.GridX + p.GridW && gy >= p.GridY && gy < p.GridY + p.GridD);
        if (MoveMode)
        {
            // Moviendo: tap en celda = intento de mover ahí (no se cambia la selección a mitad).
            if (hit < 0 || hit == _selectedIndex) MoveSelected(gx, gy);
            return;
        }
        if (hit >= 0 && hit == _selectedIndex) { Deselect(); return; }           // re-tap → deseleccionar
        if (hit >= 0) { Select(hit); return; }                                    // tocar mueble → seleccionar
        if (_selectedIndex >= 0) MoveSelected(gx, gy);                            // celda vacía con selección → mover
    }

    private void Select(int i)
    {
        _selectedIndex = i;
        SelectedCell = (_editList[i].GridX, _editList[i].GridY);
        HasSelection = true;
        OnPropertyChanged(nameof(SelectedCell));
        OnPropertyChanged(nameof(CanRotateSelected));
    }

    private void Deselect()
    {
        _selectedIndex = -1;
        SelectedCell = null;
        HasSelection = false;
        OnPropertyChanged(nameof(SelectedCell));
        OnPropertyChanged(nameof(CanRotateSelected));
    }

    private void MoveSelected(int gx, int gy)
    {
        var p = _editList[_selectedIndex];
        int nx = Math.Clamp(gx, 0, 6 - p.GridW), ny = Math.Clamp(gy, 0, 6 - p.GridD);
        if (!GameDataService.CanPlace(_editList, nx, ny, p.GridW, p.GridD, ignore: p))
        {
            // Antes fallaba en silencio: si chocaba con la celda de la mascota (3,3, sin ningún mueble
            // visible ahí) parecía un bug — "saqué al gato y seguía igual" (el gato es un mueble en OTRA
            // celda; lo que bloquea es la mascota misma, invisible en la lista de muebles). Un solo aviso
            // cubre los dos motivos de rechazo sin duplicar la regla de GameDataService.CanPlace.
            var msg = GameDataService.OverlapsPetTile(nx, ny, p.GridW, p.GridD)
                ? L.T("Ahí vive tu mascota: no puedes poner nada encima.")
                : L.T("Ahí ya hay otro mueble.");
            _ = Toast.Make(msg).Show();
            return;
        }
        p.GridX = nx;
        p.GridY = ny;
        SelectedCell = (p.GridX, p.GridY);
        OnPropertyChanged(nameof(SelectedCell));
        Placements = new List<PlacedFurniture>(_editList); // nueva ref → el diorama repinta
    }

    // Rotar no aplica a sprites de una sola vista (planta, cactus…): se oculta el botón.
    public bool CanRotateSelected => _selectedIndex >= 0 && NextView(_editList[_selectedIndex].Sprite) != _editList[_selectedIndex].Sprite;

    [RelayCommand]
    private void RotateSelected()
    {
        if (_selectedIndex < 0) return;
        var p = _editList[_selectedIndex];
        p.Sprite = NextView(p.Sprite);
        Placements = new List<PlacedFurniture>(_editList);
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (_selectedIndex < 0) return;
        _editList.RemoveAt(_selectedIndex);
        Deselect();
        Placements = new List<PlacedFurniture>(_editList);
        RefreshStored(); // el mueble reaparece en "Guardados"
    }

    // Cicla la vista del sprite: sillas 4 vistas (l→r→tl→tr), 2 vistas (l↔r), simétricos no rotan.
    private static string NextView(string sprite)
    {
        string bas = sprite; string suf = "";
        foreach (var s in new[] { "_tl", "_tr", "_l", "_r" })
            if (sprite.EndsWith(s)) { suf = s; bas = sprite[..^s.Length]; break; }
        if (suf == "") return sprite; // simétrico
        var cycle = bas == "obj_chair" ? new[] { "_l", "_r", "_tl", "_tr" } : new[] { "_l", "_r" };
        int idx = Array.IndexOf(cycle, suf);
        return bas + cycle[(idx + 1) % cycle.Length];
    }

    // Spinner mientras llega la mascota del server (cold start de Render): la pantalla anima
    // de inmediato y muestra "cargando" en vez de quedarse congelada.
    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool showClaimBanner;

    [ObservableProperty]
    private string registerPromptText = string.Empty;

    // T4-E: dispara la ceremonia si la etapa real superó a la última celebrada. Primera vez (pref sin
    // fijar) solo memoriza la etapa actual — no celebra el estado inicial de una cuenta ya existente.
    private void CheckEvolutionCelebration()
    {
        if (CurrentPet == null || IsCrystallized) return;
        int current = (int)CurrentPet.EvolutionStage;
        int lastCelebrated = Preferences.Get(LastCelebratedStageKey, -1);
        if (lastCelebrated < 0) { Preferences.Set(LastCelebratedStageKey, current); return; }
        if (current <= lastCelebrated) return;

        Preferences.Set(LastCelebratedStageKey, current);
        EvolutionText = L.F("¡{0} evolucionó a {1}!", CurrentPet.Name, StageName(CurrentPet.EvolutionStage));
        ShowEvolution = true;
    }

    [RelayCommand]
    private void DismissEvolution() => ShowEvolution = false;

    [RelayCommand]
    private async Task GoToRegister()
    {
        DismissClaimBanner();
        await Shell.Current.GoToAsync("RegisterPage");
    }

    [RelayCommand]
    private void DismissClaimBanner()
    {
        // Solo lo oculta ahora; reaparece al volver a la pestaña Mascota mientras siga siendo invitado.
        ShowClaimBanner = false;
    }

    public async Task InitializeAsync()
    {
        IsLoading = CurrentPet == null;
        IsFocusActive = _focus.IsActive; // T27-L1: refresca el banner de "volver al foco" en cada entrada
        try
        {
        if (_gameDataService.CurrentUser == null)
        {
            await _gameDataService.InitializeAsync();
        }

        if (_gameDataService.CurrentUser != null)
        {
            // T1: racha real del server; "en riesgo" = todavía sin actividad en el día local.
            StreakLabel = $"🔥 {_gameDataService.CurrentUser.CurrentStreak}";
            StreakAtRisk = _gameDataService.CurrentUser.LastActivityDate?.Date != DateTime.Now.Date;

            IsGuest = _gameDataService.CurrentUser.Email?.StartsWith("guest_") ?? false;
            RoomStyle = _gameDataService.CurrentUser.ActiveRoomStyle ?? "default";
            var inv = _gameDataService.CurrentUser.Inventory;
            Furniture = inv == null ? string.Empty
                : string.Join(",", inv.Keys.Where(FurnitureKeys.ContainsKey).Select(k => FurnitureKeys[k]));

            // Muebles colocados (F5.2). Si no hay, null → el diorama muestra su seed por defecto.
            if (!EditMode)
                Placements = _gameDataService.GetPlacements() is { Count: > 0 } pf ? pf : null;

            // El banner reaparece en cada entrada mientras sea invitado (hasta registrarse/iniciar sesión).
            ShowClaimBanner = IsGuest;

            // T7-A: etiquetas del ritual (las del usuario, o los defaults) + tablero real del server.
            var u = _gameDataService.CurrentUser;
            var labels = (u.RitualLabels ?? "").Split('|');
            for (int i = 0; i < 9; i++)
                RitualCells[i].Label = labels.Length == 9 && labels[i].Trim().Length > 0
                    ? labels[i].Trim() : DefaultRitualLabels[i];
            // El server resetea el tablero al primer toggle del día; si el último reset no fue hoy,
            // mostrarlo vacío (el estado guardado es de ayer).
            UpdateRitualGrid(u.LastRitualReset.Date == DateTime.Now.Date
                ? u.RitualGridState ?? "0,0,0,0,0,0,0,0,0" : "0,0,0,0,0,0,0,0,0");
            IsXpBuffActive = u.ActiveXpMultiplier > 1.0; // la verdad del server (línea ya consumida = off)

            // T7-C: chips de quick-log (últimas descripciones distintas del historial).
            _ = LoadQuickChipsAsync();
            
            if (_gameDataService.CurrentUser.UserPet != null)
            {
                CurrentPet = _gameDataService.CurrentUser.UserPet;
                PetStatus = CurrentPet.Status;
                RegisterPromptText = L.F("Regístrate o inicia sesión para no perder a {0}", CurrentPet.Name);

                // T5-D: celebrar la subida de XP en cuanto la mascota vuelve a estar en pantalla.
                if (_lastSeenXp >= 0 && CurrentPet.TotalXp > _lastSeenXp)
                    CelebrateXp?.Invoke();
                _lastSeenXp = CurrentPet.TotalXp;

                CheckEvolutionCelebration();
            }
        }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private readonly HttpClient _httpClient;
    private readonly GameDataService _gameDataService;
    private readonly RealtimeService _realtime;
    private readonly SettingsService _settings;

    // Lista de estados para el Picker
    public List<SyncStatus> StatusOptions { get; } = Enum.GetValues(typeof(SyncStatus)).Cast<SyncStatus>().ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private SyncStatus currentStatus;

    public Color StatusColor => CurrentStatus switch
    {
        SyncStatus.Available => Colors.Green,
        SyncStatus.Working => Colors.Orange,
        SyncStatus.Busy => Colors.Red,
        _ => Colors.Gray
    };

    // T27-L2 (#14): selector de estado con overlay del app (antes un Picker que se cortaba y salía en inglés).
    public string StatusLabel => CurrentStatus switch
    {
        SyncStatus.Available => L.T("🟢 Disponible"),
        SyncStatus.Working => L.T("🔨 Trabajando"),
        SyncStatus.Busy => L.T("🔴 Ocupado"),
        _ => L.T("⚫ Desconectado")
    };
    [ObservableProperty] private bool showStatusPicker;
    [RelayCommand] private void OpenStatusPicker() => ShowStatusPicker = true;
    [RelayCommand] private void CloseStatusPicker() => ShowStatusPicker = false;
    [RelayCommand] private void PickStatus(string s)
    {
        if (Enum.TryParse<SyncStatus>(s, out var st)) CurrentStatus = st; // dispara UpdateStatus (SignalR)
        ShowStatusPicker = false;
    }

    partial void OnCurrentStatusChanged(SyncStatus value)
    {
        UpdateStatus(value);
    }

    public async void UpdateStatus(SyncStatus newStatus)
    {
        // El estado va por SignalR (el hub lo persiste, recomputa Frenesí y difunde a las familias).
        // async void → guarda la excepción para no tumbar la app si algo no estaba protegido aguas abajo.
        try { await _realtime.SetStatusAsync(newStatus); }
        catch (Exception ex) { Console.WriteLine($"UpdateStatus failed: {ex.Message}"); }
    }
    [RelayCommand]
    public void DebugKillPet()
    {
        // Work locally without server call for testing
        PetStatus = PetStatus.Crystallized;
        OnPropertyChanged(nameof(CurrentPet));
    }

    [RelayCommand]
    public void DebugRevivePet()
    {
        // For testing visual transition only
        PetStatus = PetStatus.Alive;
        OnPropertyChanged(nameof(CurrentPet));
    }
    public ObservableCollection<RitualCellViewModel> RitualCells { get; } = new();

    [ObservableProperty]
    private bool isXpBuffActive;

    // T7-A: renombrar celdas — en este modo, tocar una celda pregunta su nombre en vez de togglear.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RitualEditText))]
    private bool ritualRenameMode;
    public string RitualEditText => RitualRenameMode ? "Listo" : "✏️";

    [RelayCommand]
    private void ToggleRitualRename() => RitualRenameMode = !RitualRenameMode;

    // T27-L2 (#12): overlay de renombrar hábito con el estilo del app (antes DisplayPromptAsync nativo).
    [ObservableProperty] private bool showRitualPrompt;
    [ObservableProperty] private string ritualPromptText = string.Empty;
    private int _renameIndex = -1;

    private void RenameCell(int index)
    {
        _renameIndex = index;
        RitualPromptText = RitualCells[index].Label;
        ShowRitualPrompt = true;
    }

    [RelayCommand]
    private async Task ConfirmRitualRename()
    {
        var name = (RitualPromptText ?? string.Empty).Trim();
        ShowRitualPrompt = false;
        if (_renameIndex < 0 || name.Length == 0 || name == RitualCells[_renameIndex].Label) return;
        RitualCells[_renameIndex].Label = name;
        await _gameDataService.SaveRitualLabelsAsync(RitualCells.Select(c => c.Label));
    }

    [RelayCommand]
    private void CancelRitualPrompt() => ShowRitualPrompt = false;

    // T7-C: quick-log — repetir un hábito reciente con 1 tap (misma IA + recompensa, sin tipear).
    public ObservableCollection<string> QuickChips { get; } = new();
    [ObservableProperty] private bool hasQuickChips;

    private async Task LoadQuickChipsAsync()
    {
        var hist = await _gameDataService.GetHistoryAsync();
        var seen = new HashSet<string>();
        var chips = new List<string>();
        foreach (var h in hist)
        {
            var d = h.Description?.Trim();
            if (string.IsNullOrEmpty(d)) continue;
            var norm = string.Join(' ', d.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (seen.Add(norm)) chips.Add(d);
            if (chips.Count == 4) break;
        }
        MainThread.BeginInvokeOnMainThread(() =>
        {
            QuickChips.Clear();
            foreach (var c in chips) QuickChips.Add(c);
            HasQuickChips = QuickChips.Count > 0;
        });
    }

    [RelayCommand]
    private async Task QuickLog(string description)
    {
        bool yes = await Shell.Current.DisplayAlert(L.T("Repetir hábito"),
            L.T("¿Registrar de nuevo?") + $"\n\n\"{description}\"", L.T("Registrar"), L.T("Cancelar"));
        if (!yes) return;
        IsLoading = true;
        try
        {
            var result = await _gameDataService.CompleteTaskAsync(description);
            // T27-L1: Long — antes el toast desaparecía demasiado rápido para leerse.
            try { await Toast.Make(result.Message ?? "Registrado.", CommunityToolkit.Maui.Core.ToastDuration.Long).Show(); } catch { }
            await InitializeAsync(); // refresca oro/hambre/racha con la verdad del server
        }
        finally { IsLoading = false; }
    }

    private static readonly string[] DefaultRitualLabels =
        { "Hacer Cama", "Beber Agua", "Estirar", "Leer 5min", "Meditar", "Limpiar", "Vitaminas", "Planificar", "Agradecer" };

    private async Task ToggleRitual(int index)
    {
        if (RitualRenameMode) { RenameCell(index); return; }
        RitualCells[index].IsCompleted = !RitualCells[index].IsCompleted; // especulativo (respuesta UI)
        try
        {
            var userId = _gameDataService.CurrentUser.Id;
            var url = $"{_settings.ServerUrl.TrimEnd('/')}/api/users/{userId}/ritual/{index}";
            var response = await _httpClient.PostAsync(url, null);
            
            if (response.IsSuccessStatusCode)
            {
                var stateStr = await response.Content.ReadAsStringAsync();
                UpdateRitualGrid(stateStr);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling ritual: {ex.Message}");
        }
    }

    private void UpdateRitualGrid(string stateStr)
    {
        // Parseo defensivo (mismo criterio que el server): basura → celda apagada.
        var parts = stateStr.Split(',');
        var state = new int[9];
        for (int i = 0; i < 9 && i < parts.Length; i++) int.TryParse(parts[i], out state[i]);

        // Sync ViewModels
        for(int i=0; i < RitualCells.Count && i < state.Length; i++)
        {
            RitualCells[i].IsCompleted = state[i] == 1;
        }

        // Check Buff (Simulated logic or fetch user again? API returns state string. Win logic checked on server.)
        // Ideally API returns user object or we derive win locally for UI speed.
        // Let's derive win locally for UI feedback.
        IsXpBuffActive = CheckWin(state);
    }
    
    private bool CheckWin(int[] s)
    {
         return (s[0]==1 && s[1]==1 && s[2]==1) || (s[3]==1 && s[4]==1 && s[5]==1) || (s[6]==1 && s[7]==1 && s[8]==1) || // Rows
                (s[0]==1 && s[3]==1 && s[6]==1) || (s[1]==1 && s[4]==1 && s[7]==1) || (s[2]==1 && s[5]==1 && s[8]==1) || // Cols
                (s[0]==1 && s[4]==1 && s[8]==1) || (s[2]==1 && s[4]==1 && s[6]==1);        // Diags
    }

    private void InitializeRitualGrid()
    {
        RitualCells.Clear();
        for (int i = 0; i < 9; i++)
        {
            RitualCells.Add(new RitualCellViewModel(i, false, DefaultRitualLabels[i], ToggleRitual));
        }
    }
}

// Un ítem de las tiras del sandbox: Guardados (Placement = null) o En el cuarto (Placement = el
// objeto colocado). El thumbnail reutiliza el sprite del paquete, igual que la tienda.
public class StoredItemVm
{
    public string Name { get; }
    public string Sprite { get; }
    public ImageSource Thumb { get; }
    public PlacedFurniture? Placement { get; }

    public StoredItemVm(string name, string sprite, PlacedFurniture? placement = null)
    {
        Name = name;
        Sprite = sprite;
        Placement = placement;
        Thumb = ImageSource.FromStream(ct => FileSystem.OpenAppPackageFileAsync($"{sprite}.png"));
    }
}
