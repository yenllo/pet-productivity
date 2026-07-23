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
    [NotifyPropertyChangedFor(nameof(StageChipLabel))]
    [NotifyPropertyChangedFor(nameof(PetSubtitle))]
    [NotifyPropertyChangedFor(nameof(CuerpoXp))]
    [NotifyPropertyChangedFor(nameof(MenteXp))]
    [NotifyPropertyChangedFor(nameof(HogarXp))]
    [NotifyPropertyChangedFor(nameof(BienestarXp))]
    [NotifyPropertyChangedFor(nameof(MoodEmoji))]
    [NotifyPropertyChangedFor(nameof(ShowMood))]
    [NotifyPropertyChangedFor(nameof(HasPet))]
    [NotifyPropertyChangedFor(nameof(ShowDangerBanner))]
    private Pet currentPet;

    // T27-L2: la sección del nombre se colapsa hasta que haya mascota (evita el hueco vacío al cargar).
    public bool HasPet => CurrentPet != null;

    // T31-9: aviso ANTES del golpe — la mecánica Fénix hoy solo se explica cuando ya te pasó.
    public bool ShowDangerBanner => HasPet && !IsCrystallized && (CurrentPet?.Health ?? 100) < 30;

    // T31: overlay explicativo ("tap = explicación") — reemplaza los toasts efímeros.
    [ObservableProperty] private bool showInfo;
    [ObservableProperty] private string infoTitle = string.Empty;
    [ObservableProperty] private string infoBody = string.Empty;

    [RelayCommand]
    private void Explain(string key)
    {
        (InfoTitle, InfoBody) = key switch
        {
            "crecimiento" => (L.T("Las 4 dimensiones"),
                L.T("Cada tarea que registras, la IA la clasifica en Cuerpo, Mente, Hogar o Bienestar y suma XP a ese círculo. Así crece tu mascota: con tu vida real, en sus 4 frentes.")),
            "vitales" => (L.T("Hambre y Salud"),
                L.T("Bajan solas con el tiempo: tu mascota te necesita a diario. Registrar tareas y focos la alimenta y la cura. Si la Salud llega a 0, se cristaliza… pero siempre puede volver.")),
            "buff" => (L.T("¡Línea completa!"),
                L.T("Completaste una línea del ritual: hoy todo tu XP se multiplica ×1.2. El tablero se reinicia cada día — vuelve mañana por otra línea.")),
            "fenix" => (L.T("El cristal Fénix"),
                L.T("Si la Salud llega a 0, tu mascota se cristaliza: no muere, se congela. Sale del cristal con esfuerzo real durante 3 días distintos o con una hazaña épica (dificultad 9+). Mejor evitarlo: registra una tarea o un foco hoy y su salud subirá. Tras revivir tiene 24 h de escudo.")),
            _ => (string.Empty, string.Empty)
        };
        ShowInfo = InfoTitle.Length > 0;
    }

    // T5: la cara del estado real (Pet.Condition, derivado de Hunger/Health). Sin burbuja en Normal.
    public string MoodEmoji => PetVisuals.MoodEmoji(CurrentPet);
    public bool ShowMood => MoodEmoji.Length > 0 && !IsCrystallized;

    // T5-D: reacción visible al ganar XP — la página anima un saltito cuando TotalXp subió
    // desde la última vez que se vio la mascota (estático: sobrevive a VMs transient).
    public event Action? CelebrateXp;
    private static double _lastSeenXp = -1;

    // T31-4: el chip de oro cuenta hacia su valor nuevo en vez de cambiar en seco
    // (mismo patrón estático que _lastSeenXp: sobrevive a VMs transient).
    [ObservableProperty] private int goldDisplay;
    private static int _lastSeenGold = -1;

    // T4-E: ceremonia de evolución (hoy Baby→Adult→Master ocurre en silencio). La etapa ya celebrada
    // se persiste en Preferences → sobrevive a que maten la app en medio (criterio 1): si al reabrir la
    // etapa real (verdad del server) sigue siendo mayor que la última celebrada, se vuelve a disparar.
    [ObservableProperty] private bool showEvolution;
    [ObservableProperty] private string evolutionText = string.Empty;
    private const string LastCelebratedStageKey = "LastCelebratedStage";

    // T1: racha diaria en la cabecera + "aún nada hoy" (punto naranja).
    [ObservableProperty] private string streakLabel = "🔥 0";
    [ObservableProperty] private bool streakAtRisk;

    // T31-2: onboarding de primera vez — 3 tarjetas que enseñan las reglas del juego
    // (hasta hoy la app no las explicaba en ningún lado). Se avanza solo con el botón.
    [ObservableProperty] private bool showOnboarding;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnboardingTitle))]
    [NotifyPropertyChangedFor(nameof(OnboardingBody))]
    [NotifyPropertyChangedFor(nameof(OnboardingDots))]
    [NotifyPropertyChangedFor(nameof(OnboardingButton))]
    private int onboardingStep;

    public string OnboardingTitle => OnboardingStep switch
    {
        0 => L.T("Así se juega"),
        1 => L.T("Cuídala a diario"),
        _ => L.T("El ritual diario")
    };

    public string OnboardingBody => OnboardingStep switch
    {
        0 => L.T("Cuéntale a la app lo que hiciste hoy, con tus palabras. Una IA lo juzgará: le pondrá dificultad y te dará XP (crece tu mascota) y Oro (decoras su cuarto)."),
        1 => L.T("El Hambre y la Salud bajan solos cada día. Registrar tareas y focos la alimenta y la cura. Si la descuidas mucho, se cristaliza… y rescatarla cuesta esfuerzo de verdad."),
        _ => L.T("El tablero 3×3 es un tres-en-raya de mini-hábitos: marca 3 en línea y TODO tu XP de hoy vale ×1.2. Puedes renombrar cada celda con el ✏️ para poner tus hábitos reales.")
    };

    public string OnboardingDots => OnboardingStep switch { 0 => "● ○ ○", 1 => "○ ● ○", _ => "○ ○ ●" };
    public string OnboardingButton => OnboardingStep < 2 ? L.T("Siguiente ›") : L.T("¡A jugar!");

    public void StartOnboarding()
    {
        OnboardingStep = 0;
        ShowOnboarding = true;
    }

    [RelayCommand]
    private void NextOnboarding()
    {
        if (OnboardingStep < 2) { OnboardingStep++; return; }
        ShowOnboarding = false;
        Onboarding.MarkSeen("Dashboard");
    }

    // T27-L2 (#18): el chip 🔥 no se entendía y el punto naranja parecía notificación → tap = explicación.
    // T31: migrado del toast (desaparecía rápido) al overlay explicativo.
    [RelayCommand]
    private void ExplainStreak()
    {
        var n = _gameDataService.CurrentUser?.CurrentStreak ?? 0;
        InfoTitle = L.T("Racha diaria");
        InfoBody = StreakAtRisk
            ? L.F("Racha diaria: {0} día(s) seguidos haciendo algo. El punto naranja avisa que HOY aún no registras nada — ¡haz una tarea o un foco para mantenerla!", n)
            : L.F("Racha diaria: {0} día(s) seguidos haciendo algo. ¡Hoy ya está asegurada! ✔", n);
        ShowInfo = true;
    }

    // Sprites de cada etapa de la especie actual (previsualización de evolución).
    public string BabySprite => PetVisuals.SpriteFor(CurrentPet?.Species ?? PetSpecies.Sprout, EvolutionStage.Baby);
    public string AdultSprite => PetVisuals.SpriteFor(CurrentPet?.Species ?? PetSpecies.Sprout, EvolutionStage.Adult);
    public string MasterSprite => PetVisuals.SpriteFor(CurrentPet?.Species ?? PetSpecies.Sprout, EvolutionStage.Master);

    // Datos reales para la cabecera y los anillos (antes placeholders).
    // El chip mostraba "Nv {TotalXp/1000+1}" — un nivel inventado y desconectado de la etapa real
    // (Huevo/Cría/Adulto/Maestro) que el subtítulo de abajo muestra en la MISMA pantalla: un Maestro
    // podía leer "Nv 3" arriba y "Maestro" abajo. Ahora ambos usan la misma etapa (ver PetVisuals).
    public string StageChipLabel => CurrentPet == null ? "" : PetVisuals.StageName(CurrentPet.EvolutionStage);
    public string PetSubtitle => CurrentPet == null ? "" : $"{SpeciesName(CurrentPet.Species)} · {PetVisuals.StageName(CurrentPet.EvolutionStage)}";
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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCrystallized))]
    [NotifyPropertyChangedFor(nameof(PetOpacity))]
    [NotifyPropertyChangedFor(nameof(PetImageSource))]
    [NotifyPropertyChangedFor(nameof(ShowMood))]
    [NotifyPropertyChangedFor(nameof(ShowDangerBanner))]
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
    [NotifyPropertyChangedFor(nameof(PadVisible))]
    [NotifyPropertyChangedFor(nameof(SandboxHintVisible))]
    private bool editMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PadVisible))]
    [NotifyPropertyChangedFor(nameof(SandboxHintVisible))]
    private bool hasSelection;

    // Seleccionar = mover: el pad aparece con la selección, sin paso intermedio "Mover".
    public bool PadVisible => EditMode && HasSelection;
    public bool SandboxHintVisible => EditMode && !HasSelection;

    // Celda origen del mueble seleccionado (para resaltar en el diorama). null = ninguno.
    // Wall = objeto colgado (puede compartir celda de borde con un mueble de piso).
    public (int X, int Y, bool Wall)? SelectedCell { get; private set; }

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
        new() { Name = p.Name, Sprite = p.Sprite, GridX = p.GridX, GridY = p.GridY, GridW = p.GridW, GridD = p.GridD, OnWall = p.OnWall };

    // Lápiz ✏️ → abre el sandbox. Se edita sobre CLONES: los objetos canónicos del usuario no se
    // mutan, así "Cancelar" es simplemente descartar la lista de trabajo.
    [RelayCommand]
    private async Task EnterSandbox()
    {
        var cur = _gameDataService.GetPlacements();
        _editList = cur is { Count: > 0 } ? cur.Select(Clone).ToList() : _gameDataService.SeedPlacements();
        Deselect();
        Placements = new List<PlacedFurniture>(_editList);
        EditMode = true;
        _catalogCache ??= await _gameDataService.GetCatalogAsync();
        MigrateWallLegacy();
        RefreshStored();
    }

    // ponytail: migración one-time — cuadros/ventanas comprados antes del slot "wall" quedaron en el
    // piso; al abrir el editor se cuelgan en el hueco de pared más cercano (o caen a Guardados si no
    // queda ninguno). Se persiste recién al Guardar.
    private void MigrateWallLegacy()
    {
        var wallNames = _catalogCache?.Where(i => i.Slot == "wall").Select(i => i.Name).ToHashSet();
        if (wallNames == null || wallNames.Count == 0) return;
        bool changed = false;
        foreach (var p in _editList.Where(p => !p.OnWall && wallNames.Contains(p.Name)).ToList())
        {
            p.OnWall = true; p.GridW = 1; p.GridD = 1;
            changed = true;
            if (!GameDataService.CanPlaceWall(_editList, p.GridX, p.GridY, ignore: p))
            {
                (int, int)? best = null; int bd = int.MaxValue;
                for (int x = 0; x < 6; x++)
                    for (int y = 0; y < 6; y++)
                    {
                        if (!GameDataService.CanPlaceWall(_editList, x, y, ignore: p)) continue;
                        int dist = Math.Abs(x - p.GridX) + Math.Abs(y - p.GridY);
                        if (dist < bd) { bd = dist; best = (x, y); }
                    }
                if (best == null) { _editList.Remove(p); continue; }
                (p.GridX, p.GridY) = best.Value;
            }
            p.Sprite = GameDataService.WallView(p.Sprite, p.GridX, p.GridY);
        }
        if (changed) Placements = new List<PlacedFurniture>(_editList);
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
        EditMode = false;
    }

    // Tap en la tira "En el cuarto" → seleccionar ese objeto en el diorama.
    [RelayCommand]
    private void SelectPlaced(StoredItemVm item)
    {
        if (item?.Placement == null) return;
        int i = _editList.IndexOf(item.Placement);
        if (i >= 0) Select(i);
    }

    // ---- Mover (seleccionar = mover, sin paso intermedio) ----
    // Respaldo para ✕: posición, vista y huella de cuando se seleccionó (rotar intercambia W×D).
    private (int X, int Y, string Sprite, int W, int D) _moveBackup;

    // Flechas del pad: 1 celda por los ejes de la grilla iso (en pantalla son diagonales).
    [RelayCommand]
    private void MoveStep(string dir)
    {
        if (_selectedIndex < 0) return;
        var p = _editList[_selectedIndex];
        var (dx, dy) = dir switch { "up" => (0, -1), "down" => (0, 1), "left" => (-1, 0), _ => (1, 0) };
        MoveSelected(p.GridX + dx, p.GridY + dy);
    }

    // ✕ del pad: revierte posición, vista y huella al estado de cuando se seleccionó.
    [RelayCommand]
    private void CancelMove()
    {
        if (_selectedIndex >= 0)
        {
            var p = _editList[_selectedIndex];
            (p.GridX, p.GridY, p.Sprite, p.GridW, p.GridD) = _moveBackup;
            Placements = new List<PlacedFurniture>(_editList);
        }
        Deselect();
    }

    [RelayCommand]
    private void ConfirmMove() => Deselect();

    // Tap en un guardado → a la primera celda libre (o hueco de pared), y queda seleccionado para moverlo.
    [RelayCommand]
    private async Task PlaceStored(StoredItemVm item)
    {
        if (!EditMode || item == null) return;
        var cat = _catalogCache?.FirstOrDefault(i => i.Name == item.Name);
        if (cat?.Slot == "wall")
        {
            var wc = GameDataService.FindFreeWallCell(_editList);
            if (wc == null) { await Toast.Make(L.T("Las paredes están llenas: quita algún cuadro primero.")).Show(); return; }
            _editList.Add(new PlacedFurniture { Name = item.Name, Sprite = GameDataService.WallView(item.Sprite, wc.Value.x, wc.Value.y),
                                                GridX = wc.Value.x, GridY = wc.Value.y, OnWall = true });
        }
        else
        {
            var (w, d) = cat != null && (cat.GridW > 1 || cat.GridD > 1)
                ? (cat.GridW, cat.GridD)
                : GameDataService.FootprintFor(item.Sprite); // fallback: Freebies y catálogo sin footprint
            bool isRug = cat?.Slot == "rug";
            var cell = GameDataService.FindFreeCell(_editList, w, d, floorDecor: isRug);
            if (cell == null) { await Toast.Make(L.T("No hay espacio: mueve o quita algo primero.")).Show(); return; }
            _editList.Add(new PlacedFurniture { Name = item.Name, Sprite = item.Sprite, GridX = cell.Value.x, GridY = cell.Value.y, GridW = w, GridD = d, IsFloorDecor = isRug });
        }
        Placements = new List<PlacedFurniture>(_editList);
        Select(_editList.Count - 1);
        RefreshStored();
    }

    // La página bridgea el toque del diorama (celda de piso) a esto.
    public void OnCellTapped(int gx, int gy)
    {
        if (!EditMode) return;
        int hit = HitTest(gx, gy);
        if (hit >= 0 && hit == _selectedIndex) { Deselect(); return; }  // re-tap → deseleccionar
        if (hit >= 0) { Select(hit); return; }                          // tocar otro objeto → cambiar selección
        if (_selectedIndex >= 0) MoveSelected(gx, gy);                  // celda libre con selección → mover ahí
    }

    // Piso primero; si no hay, un colgado en esa celda de riel exacta.
    private int HitTest(int gx, int gy)
    {
        int hit = _editList.FindIndex(p => !p.OnWall && gx >= p.GridX && gx < p.GridX + p.GridW && gy >= p.GridY && gy < p.GridY + p.GridD);
        return hit >= 0 ? hit : _editList.FindIndex(p => p.OnWall && p.GridX == gx && p.GridY == gy);
    }

    // ---- Arrastre directo (imán a celda): el mueble sigue al dedo celda a celda ----
    // El Select se difiere al PRIMER cruce de celda: si se hiciera en Pressed, el tap normal
    // (press+release en la misma celda) llegaría a OnCellTapped con el objeto ya seleccionado
    // y el toggle lo des-seleccionaría — se rompería "tap para seleccionar".
    // Offset presión→ancla: una cama 2×2 agarrada por la esquina no salta al ancla.
    private bool _dragging;
    private int _dragHit = -1;
    private (int X, int Y) _dragOffset;

    public void OnDragStarted(int gx, int gy)
    {
        if (!EditMode) return;
        _dragHit = HitTest(gx, gy); // -1 = celda vacía → no hay drag; el release cae al flujo tap
        if (_dragHit < 0) return;
        var p = _editList[_dragHit];
        _dragOffset = (gx - p.GridX, gy - p.GridY);
    }

    public void OnDragMoved(int gx, int gy)
    {
        if (!EditMode || _dragHit < 0) return;
        if (!_dragging) { _dragging = true; if (_dragHit != _selectedIndex) Select(_dragHit); }
        // Silencioso: celda inválida = el mueble no avanza (sin Toast/flash por cada celda cruzada).
        MoveSelected(gx - _dragOffset.X, gy - _dragOffset.Y);
    }

    // La selección se mantiene: el pad queda para ajuste fino/rotar/✓, igual que tras tap-destino.
    public void OnDragEnded() { _dragging = false; _dragHit = -1; }

    private void Select(int i)
    {
        _selectedIndex = i;
        var p = _editList[i];
        _moveBackup = (p.GridX, p.GridY, p.Sprite, p.GridW, p.GridD); // para ✕ (revertir)
        SelectedCell = (p.GridX, p.GridY, p.OnWall);
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

    // La página lo suscribe al FlashInvalid del diorama (rombo rojo de rechazo, además del Toast).
    public event Action<int, int, int, int>? InvalidMove;

    private void MoveSelected(int gx, int gy)
    {
        var p = _editList[_selectedIndex];
        if (p.OnWall)
        {
            // Colgado: solo celdas de riel (bordes traseros); la pared destino fija la vista.
            int wx = Math.Clamp(gx, 0, 5), wy = Math.Clamp(gy, 0, 5);
            if (!GameDataService.CanPlaceWall(_editList, wx, wy, ignore: p))
            {
                if (_dragging) return; // drag: el objeto simplemente no avanza, sin regañar por celda
                var wmsg = GameDataService.IsRailCell(wx, wy)
                    ? L.T("Ahí ya hay otro objeto colgado.")
                    : L.T("Los objetos de pared van en las paredes del fondo.");
                _ = Toast.Make(wmsg).Show();
                InvalidMove?.Invoke(wx, wy, 1, 1);
                return;
            }
            p.GridX = wx;
            p.GridY = wy;
            p.Sprite = GameDataService.WallView(p.Sprite, wx, wy);
            SelectedCell = (wx, wy, true);
            OnPropertyChanged(nameof(SelectedCell));
            Placements = new List<PlacedFurniture>(_editList);
            return;
        }
        int nx = Math.Clamp(gx, 0, 6 - p.GridW), ny = Math.Clamp(gy, 0, 6 - p.GridD);
        if (!GameDataService.CanPlace(_editList, nx, ny, p.GridW, p.GridD, ignore: p, floorDecor: p.IsFloorDecor))
        {
            if (_dragging) return; // drag: sin Toast/flash por cada celda inválida cruzada
            // Antes fallaba en silencio: si chocaba con la celda de la mascota (3,3, sin ningún mueble
            // visible ahí) parecía un bug — "saqué al gato y seguía igual" (el gato es un mueble en OTRA
            // celda; lo que bloquea es la mascota misma, invisible en la lista de muebles). Un solo aviso
            // cubre los dos motivos de rechazo sin duplicar la regla de GameDataService.CanPlace.
            var msg = GameDataService.OverlapsPetTile(nx, ny, p.GridW, p.GridD)
                ? L.T("Ahí vive tu mascota: no puedes poner nada encima.")
                : L.T("Ahí ya hay otro mueble.");
            _ = Toast.Make(msg).Show();
            InvalidMove?.Invoke(nx, ny, p.GridW, p.GridD);
            return;
        }
        p.GridX = nx;
        p.GridY = ny;
        SelectedCell = (p.GridX, p.GridY, false);
        OnPropertyChanged(nameof(SelectedCell));
        Placements = new List<PlacedFurniture>(_editList); // nueva ref → el diorama repinta
    }

    // Rotar no aplica a sprites de una sola vista (planta, cactus…) ni a colgados (la pared fija la vista).
    public bool CanRotateSelected => _selectedIndex >= 0 && !_editList[_selectedIndex].OnWall
        && NextView(_editList[_selectedIndex].Sprite) != _editList[_selectedIndex].Sprite;

    [RelayCommand]
    private void RotateSelected()
    {
        if (_selectedIndex < 0) return;
        var p = _editList[_selectedIndex];
        var ns = NextView(p.Sprite);
        if (ns == p.Sprite) return;
        // Girar entre vistas _l/_r = orientación perpendicular: la huella W×D se intercambia
        // (no-op en huellas cuadradas). Si girado no cabe, se avisa y no se rota.
        int nw = p.GridD, nd = p.GridW;
        if (!GameDataService.CanPlace(_editList, p.GridX, p.GridY, nw, nd, ignore: p, floorDecor: p.IsFloorDecor))
        {
            _ = Toast.Make(L.T("No cabe girado ahí: muévelo primero.")).Show();
            InvalidMove?.Invoke(p.GridX, p.GridY, nw, nd);
            return;
        }
        p.Sprite = ns;
        p.GridW = nw;
        p.GridD = nd;
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
        EvolutionText = L.F("¡{0} evolucionó a {1}!", CurrentPet.Name, PetVisuals.StageName(CurrentPet.EvolutionStage));
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

                // T31-4: contador animado del oro (solo cuando cambia respecto a lo último visto).
                int gold = CurrentPet.GoldCoins;
                if (_lastSeenGold >= 0 && gold != _lastSeenGold)
                    _ = Anim.CountAsync(_lastSeenGold, gold, v => GoldDisplay = v);
                else
                    GoldDisplay = gold;
                _lastSeenGold = gold;

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

    // T31-5: la página escucha estos eventos para celebrar (pop de celda / destello de línea).
    // Solo la acción del usuario dispara el pop — la carga desde el server entra en silencio.
    public event Action<int>? RitualCellPopped;
    public event Action<int[]>? RitualLineCompleted;

    private async Task ToggleRitual(int index)
    {
        if (RitualRenameMode) { RenameCell(index); return; }
        RitualCells[index].IsCompleted = !RitualCells[index].IsCompleted; // especulativo (respuesta UI)
        if (RitualCells[index].IsCompleted) RitualCellPopped?.Invoke(index);
        try
        {
            var userId = _gameDataService.CurrentUser.Id;
            var url = $"{_settings.ServerUrl.TrimEnd('/')}/api/users/{userId}/ritual/{index}";
            var response = await _httpClient.PostAsync(url, null);
            
            if (response.IsSuccessStatusCode)
            {
                var stateStr = await response.Content.ReadAsStringAsync();
                UpdateRitualGrid(stateStr, celebrate: true); // acción del usuario → puede celebrar línea
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling ritual: {ex.Message}");
        }
    }

    // celebrate=false en cargas (página/refresh): solo el toggle del usuario celebra la línea.
    private void UpdateRitualGrid(string stateStr, bool celebrate = false)
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
        // T31-5: celebrar SOLO la transición sin-línea → línea, y solo por acción del usuario.
        bool wasActive = IsXpBuffActive;
        var line = WinLine(state);
        IsXpBuffActive = line != null;
        if (celebrate && !wasActive && line != null) RitualLineCompleted?.Invoke(line);
    }

    private bool CheckWin(int[] s) => WinLine(s) != null;

    // T31-5: devuelve la primera línea completa (índices) o null — la celebración necesita saber CUÁL.
    private static int[]? WinLine(int[] s)
    {
        int[][] lines =
        {
            new[]{0,1,2}, new[]{3,4,5}, new[]{6,7,8},   // filas
            new[]{0,3,6}, new[]{1,4,7}, new[]{2,5,8},   // columnas
            new[]{0,4,8}, new[]{2,4,6}                  // diagonales
        };
        return lines.FirstOrDefault(l => l.All(i => s[i] == 1));
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
