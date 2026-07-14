using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PetProductivity.Shared.Models;
using SkiaSharp;

namespace PetProductivity.Client.Services;

public class GameDataService
{
    public User? CurrentUser { get; private set; }

    public void SetUser(User? user)
    {
        CurrentUser = user;
    }

    public List<TaskItem> TaskHistory { get; private set; } = new();

    private readonly HttpClient _httpClient;
    private readonly AuthService _authService;
    private readonly SettingsService _settingsService;
    private readonly ILogger<GameDataService> _log;
    private readonly INotificationService _notify;

    public GameDataService(HttpClient httpClient, AuthService authService, SettingsService settingsService,
        ILogger<GameDataService> log, INotificationService notify)
    {
        _httpClient = httpClient;
        _authService = authService;
        _settingsService = settingsService;
        _log = log;
        _notify = notify;
    }

    // ── T17: helper HTTP único ─────────────────────────────────────────────────
    // Punto ÚNICO de URL + serialización + manejo de error con log real (Console.WriteLine es
    // invisible en Android). Aquí se enchufan después el retry y la cola offline de T13.

    private string Url(string path) => $"{_settingsService.ServerUrl.TrimEnd('/')}{path}";

    private async Task<T?> GetAsync<T>(string path)
    {
        try { return await _httpClient.GetFromJsonAsync<T>(Url(path)); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "GET {Path} falló", path);
            return default;
        }
    }

    private async Task<(T? Data, string? Error)> PostAsync<T>(string path, object body)
    {
        try
        {
            var resp = await _httpClient.PostAsJsonAsync(Url(path), body);
            if (resp.IsSuccessStatusCode) return (await resp.Content.ReadFromJsonAsync<T>(), null);
            var msg = await resp.Content.ReadAsStringAsync();
            _log.LogWarning("POST {Path} → {Status}: {Msg}", path, (int)resp.StatusCode, msg);
            return (default, string.IsNullOrWhiteSpace(msg) ? $"Error {(int)resp.StatusCode}" : msg);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "POST {Path} falló", path);
            return (default, ConnectionError);
        }
    }

    // Variante sin cuerpo de respuesta (solo ok/mensaje).
    private async Task<(bool Ok, string? Error)> PostAsync(string path, object body)
    {
        try
        {
            var resp = await _httpClient.PostAsJsonAsync(Url(path), body);
            if (resp.IsSuccessStatusCode) return (true, null);
            var msg = await resp.Content.ReadAsStringAsync();
            _log.LogWarning("POST {Path} → {Status}: {Msg}", path, (int)resp.StatusCode, msg);
            return (false, string.IsNullOrWhiteSpace(msg) ? $"Error {(int)resp.StatusCode}" : msg);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "POST {Path} falló", path);
            return (false, ConnectionError);
        }
    }

    // ── T13: cola offline (solo los envíos irreemplazables: tarea y focus/complete) ──
    // Encola INTENCIONES, no estado (server sigue siendo la única verdad). Preferences = mismo
    // mecanismo que AuthToken; nada de SQLite ni JSON de estado.

    // Sentinela: distingue "sin red / server caído" (reintentable) de un rechazo real del server.
    internal static string ConnectionError => L.T("Error de conexión. Inténtalo de nuevo.");
    private const string QueueKey = "OfflineSendQueue";
    private const int QueueCap = 20;
    private static readonly TimeSpan QueueTtl = TimeSpan.FromHours(48); // no premiar arqueología
    private bool _draining;

    private class QueuedSend
    {
        public string Kind { get; set; } = "task"; // "task" | "focus"
        public string Description { get; set; } = "";
        public Guid PetId { get; set; }
        public Guid SessionId { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    private static List<QueuedSend> LoadQueue()
    {
        try { return JsonSerializer.Deserialize<List<QueuedSend>>(Preferences.Get(QueueKey, "[]")) ?? new(); }
        catch { return new(); }
    }

    private static void SaveQueue(List<QueuedSend> q) => Preferences.Set(QueueKey, JsonSerializer.Serialize(q));

    private void Enqueue(QueuedSend s)
    {
        var q = LoadQueue();
        q.Add(s);
        while (q.Count > QueueCap) q.RemoveAt(0); // tope: se descarta lo más viejo
        SaveQueue(q);
        _log.LogInformation("Cola offline: encolado {Kind} ({Count} pendientes)", s.Kind, q.Count);
        // Cold start de Render CON wifi: no habrá evento de conectividad que drene — reintentar solo.
        _ = Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(_ => DrainQueueAsync());
    }

    // Al abrir la app: ping de calentamiento (cold start de Render) + drenar pendientes.
    // Además se drena solo al volver la conectividad (MAUI Connectivity).
    public void StartOfflineQueue()
    {
        _ = WarmUpAsync();
        Connectivity.ConnectivityChanged += (_, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet) _ = DrainQueueAsync();
        };
        _ = DrainQueueAsync();
    }

    private async Task WarmUpAsync()
    {
        try { await _httpClient.GetAsync(Url("/health")); } catch { /* best-effort */ }
    }

    public async Task DrainQueueAsync()
    {
        if (_draining) return; // un drenado a la vez (arranque + evento de conectividad pueden solaparse)
        _draining = true;
        try
        {
            var q = LoadQueue();
            int before = q.Count;
            q.RemoveAll(s => DateTime.UtcNow - s.CreatedUtc > QueueTtl);
            if (q.Count != before) SaveQueue(q);
            if (q.Count == 0) return;

            bool sentAny = false;
            while (q.Count > 0)
            {
                var s = q[0];
                HttpResponseMessage resp;
                try
                {
                    // Confirmed=true: en background no se puede preguntar; fuera de contexto = ×0.25 y ya.
                    resp = s.Kind == "focus"
                        ? await _httpClient.PostAsJsonAsync(Url("/api/focus/complete"), new { s.SessionId, s.Description })
                        : await _httpClient.PostAsJsonAsync(Url("/api/tasks"), new { s.PetId, Confirmed = true, s.Description, Language = L.Lang });
                }
                catch { break; } // sigue sin red: se reintenta en la próxima conexión
                if ((int)resp.StatusCode >= 500) break; // server enfermo: reintentar después (el TTL acota)

                q.RemoveAt(0); // 2xx o 4xx (rechazo real, p. ej. sesión ya cerrada): no reintentar
                SaveQueue(q);
                if (resp.IsSuccessStatusCode)
                {
                    sentAny = true;
                    string reward = "";
                    try
                    {
                        var r = await resp.Content.ReadFromJsonAsync<TaskResult>();
                        if (r?.XpEarned > 0) reward = L.F(" +{0} XP · +{1} Oro", r.XpEarned, r.GoldEarned);
                    }
                    catch { }
                    var desc = s.Description.Length > 40 ? s.Description[..40] + "…" : s.Description;
                    try { _notify.ShowNotification(L.T("📤 Enviado"), L.F("\"{0}\" llegó al servidor.{1}", desc, reward)); } catch { }
                }
            }
            if (sentAny) await InitializeAsync(forceRefresh: true);
        }
        finally { _draining = false; }
    }

    // Hay sesión utilizable aunque /me haya fallado sin red: el Bearer sale de Preferences.
    private bool HasSession => _authService.IsLoggedIn || !string.IsNullOrEmpty(_authService.CurrentToken);

    // ── Usuario ────────────────────────────────────────────────────────────────

    // forceRefresh=false (arranque): reusa el usuario ya traído por EnsureGuestOrLoggedInAsync (/me o
    // registro/login) → un solo round-trip. forceRefresh=true (tras una acción): re-consulta la verdad
    // del server. Evita el doble fetch /me + /{id} en cada apertura.
    public async Task InitializeAsync(bool forceRefresh = false)
    {
        await _authService.EnsureGuestOrLoggedInAsync();

        if (CurrentUser == null) CurrentUser = _authService.CurrentUser;
        if (!forceRefresh && CurrentUser != null) return;

        if (_authService.IsLoggedIn)
            CurrentUser = await GetAsync<User>($"/api/users/{_authService.CurrentUser.Id}") ?? _authService.CurrentUser;
    }

    // ── Tareas ─────────────────────────────────────────────────────────────────

    // petId vacío = mascota personal. confirmed=true registra una tarea fuera de contexto (recompensa reducida).
    public async Task<TaskResult> CompleteTaskAsync(string description, Guid petId = default, bool confirmed = false)
    {
        if (CurrentUser == null) await InitializeAsync();
        if (!HasSession)
            return new TaskResult { Message = L.T("Error de conexión con el servidor. No se pudo evaluar la tarea.") };

        // T17: el contrato ES TaskResult (Shared) — sin parseo a mano.
        var body = new { PetId = petId, Confirmed = confirmed, Description = description, Language = L.Lang };
        var (result, error) = await PostAsync<TaskResult>("/api/tasks", body);
        if (result == null && error == ConnectionError)
        {
            // T13-A: un reintento corto cubre micro-cortes y el cold start (ya calentado por /health).
            // Riesgo de duplicado acotado: el server dedupe misma descripción en 24 h (×0.1).
            await Task.Delay(2000);
            (result, error) = await PostAsync<TaskResult>("/api/tasks", body);
        }
        if (result == null && error == ConnectionError)
        {
            // T13-B: sin red — la intención se encola y se envía sola al reconectar.
            Enqueue(new QueuedSend { Kind = "task", Description = description, PetId = petId, CreatedUtc = DateTime.UtcNow });
            return new TaskResult { Queued = true, Message = L.T("📡 Sin conexión: tu tarea quedó guardada y se enviará sola al reconectar.") };
        }
        if (result == null)
            return new TaskResult { Message = error ?? L.T("Error de conexión con el servidor. No se pudo evaluar la tarea.") };

        if (result.NeedsConfirmation) return result;

        await InitializeAsync(forceRefresh: true); // la verdad del server
        // La UI muestra el feedback emocional como mensaje principal (salvo en un revivir).
        if (!result.IsRevived && !string.IsNullOrWhiteSpace(result.EmotionalFeedback))
            result.Message = result.EmotionalFeedback;

        TaskHistory.Insert(0, new TaskItem { Description = description, IsCompleted = true, AiDifficultyScore = result.DifficultyScore });
        return result;
    }

    // T7: guarda los nombres de las 9 celdas del ritual (el tablero de hábitos personal).
    public async Task<bool> SaveRitualLabelsAsync(IEnumerable<string> labels)
    {
        var (ok, _) = await PostAsync("/api/users/me/ritual-labels", new { Labels = labels.ToList() });
        return ok;
    }

    // ── Foco (AC3) ─────────────────────────────────────────────────────────────

    public async Task<Guid?> StartFocusAsync(Guid petId, int targetMinutes)
    {
        if (CurrentUser == null) await InitializeAsync();
        if (!_authService.IsLoggedIn) return null;
        var (r, _) = await PostAsync<FocusStartResponse>("/api/focus/start", new { PetId = petId, TargetMinutes = targetMinutes });
        return r?.SessionId;
    }

    public async Task CancelFocusAsync(Guid sessionId)
    {
        if (!_authService.IsLoggedIn) return;
        await PostAsync("/api/focus/cancel", new { SessionId = sessionId });
    }

    // Comprobante: comprime la foto a JPEG ~512px y la sube; devuelve el veredicto (✓/✗) o null.
    public async Task<bool?> UploadProofAsync(Guid sessionId, string description, Stream photo)
    {
        if (!_authService.IsLoggedIn) return null;
        // T25: decode+resize de una foto de cámara (~12 MP) fuera del hilo UI (congelaba la captura).
        var jpeg = await Task.Run(() => ResizeJpeg(photo, 512, 80));
        if (jpeg.Length == 0) return null;
        var (r, _) = await PostAsync<ProofResponse>("/api/focus/proof", new
        {
            SessionId = sessionId,
            ImageBase64 = Convert.ToBase64String(jpeg),
            MimeType = "image/jpeg",
            Description = description
        });
        return r?.Plausible;
    }

    private static byte[] ResizeJpeg(Stream input, int maxSize, int quality)
    {
        using var original = SKBitmap.Decode(input);
        if (original == null) return Array.Empty<byte>();
        float scale = Math.Min(1f, (float)maxSize / Math.Max(original.Width, original.Height));
        var info = new SKImageInfo(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale)));
        // SkiaSharp 4.x: SKFilterQuality eliminado; lineal ≈ Medium de antes.
        using var resized = original.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)) ?? original;
        using var img = SKImage.FromBitmap(resized);
        using var data = img.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    // Historial laboral (server): tareas con datos del comprobante. Con groupId → historial del grupo.
    public async Task<List<HistoryItem>> GetHistoryAsync(Guid? groupId = null)
    {
        if (CurrentUser == null) await InitializeAsync();
        if (!_authService.IsLoggedIn) return new();
        var path = groupId == null ? "/api/focus/history" : $"/api/focus/group/{groupId}/history";
        return await GetAsync<List<HistoryItem>>(path) ?? new();
    }

    // ── Foco grupal (F3) ───────────────────────────────────────────────────────

    public async Task<GroupFocusInfo?> StartGroupFocusAsync(Guid groupId, int target, string topic)
    {
        if (!_authService.IsLoggedIn) return null;
        var (r, _) = await PostAsync<GroupFocusInfo>("/api/focus/group/start",
            new { GroupId = groupId, TargetMinutes = target, Description = topic });
        return r;
    }

    public async Task<GroupFocusInfo?> JoinGroupFocusAsync(Guid groupFocusId)
    {
        if (!_authService.IsLoggedIn) return null;
        var (r, _) = await PostAsync<GroupFocusInfo>("/api/focus/group/join", new { GroupFocusId = groupFocusId });
        return r;
    }

    public async Task<ActiveGroupFocus?> GetActiveGroupFocusAsync(Guid groupId)
    {
        if (!_authService.IsLoggedIn) return null;
        return await GetAsync<ActiveGroupFocus>($"/api/focus/group/active/{groupId}");
    }

    // Bytes de la imagen de un comprobante (con el Bearer del HttpClient).
    public async Task<byte[]?> GetProofImageAsync(Guid proofId)
    {
        if (!_authService.IsLoggedIn) return null;
        try
        {
            var resp = await _httpClient.GetAsync(Url($"/api/focus/proof/{proofId}"));
            if (resp.IsSuccessStatusCode) return await resp.Content.ReadAsByteArrayAsync();
            _log.LogWarning("GET proof {Id} → {Status}", proofId, (int)resp.StatusCode);
        }
        catch (Exception ex) { _log.LogWarning(ex, "GET proof {Id} falló", proofId); }
        return null;
    }

    public async Task<TaskResult> CompleteFocusAsync(Guid sessionId, string description)
    {
        if (!HasSession) return new TaskResult { Message = L.T("Sesión inválida.") };
        var body = new { SessionId = sessionId, Description = description };
        var (r, error) = await PostAsync<FocusCompleteResponse>("/api/focus/complete", body);
        if (r == null && error == ConnectionError)
        {
            await Task.Delay(2000); // T13-A: mismo reintento corto que el submit de tarea
            (r, error) = await PostAsync<FocusCompleteResponse>("/api/focus/complete", body);
        }
        if (r == null && error == ConnectionError)
        {
            // T13-B: el server ya midió el tiempo (StartedAt); FocusMath premia por target aunque
            // el complete llegue tarde. La sesión no expira server-side, así que el TTL de 48 h manda.
            Enqueue(new QueuedSend { Kind = "focus", SessionId = sessionId, Description = description, CreatedUtc = DateTime.UtcNow });
            return new TaskResult { Queued = true, Message = L.T("📡 Sin conexión: tu foco quedó guardado y se enviará solo al reconectar.") };
        }
        if (r == null) return new TaskResult { Message = error ?? L.T("Error de conexión.") };

        await InitializeAsync(forceRefresh: true);
        return new TaskResult
        {
            DifficultyScore = r.DifficultyScore,
            XpEarned = r.XpEarned,
            GoldEarned = r.GoldEarned,
            Message = r.Message
        };
    }

    // ── Tienda ─────────────────────────────────────────────────────────────────

    // Devuelve (ok, mensaje): distingue "sin oro" (lo dice el server) de un fallo de red/servidor.
    public async Task<(bool ok, string message)> BuyItemAsync(string itemName, int price, string description)
    {
        if (CurrentUser == null) await InitializeAsync();
        // El server ignora precio y userId (catálogo + token son la verdad); solo viaja el nombre.
        var (ok, error) = await PostAsync("/api/shop/buy", new { ItemName = itemName });
        if (ok) { await InitializeAsync(forceRefresh: true); return (true, string.Empty); }
        return (false, error ?? "No se pudo completar la compra.");
    }

    // Compra Premium con dinero real (F5.4). Hoy manda un recibo stub ("dev-stub"); con billing real irá el
    // purchase-token de Google Play. El server valida el recibo antes de otorgar.
    public async Task<(bool ok, string message)> PurchasePremiumAsync(string itemName)
    {
        if (CurrentUser == null) await InitializeAsync();
        var (ok, error) = await PostAsync("/api/shop/purchase-premium", new { ItemName = itemName, Receipt = "dev-stub" });
        if (ok) { await InitializeAsync(forceRefresh: true); return (true, string.Empty); }
        return (false, error ?? "No se pudo completar la compra premium.");
    }

    public async Task<List<ShopItem>> GetCatalogAsync() =>
        await GetAsync<List<ShopItem>>("/api/shop/catalog") ?? new();

    public string GetActiveStyle() => CurrentUser?.ActiveRoomStyle ?? "default";

    // ---- F5.2 colocación de muebles (cosmético) ----
    public List<PlacedFurniture> GetPlacements() => CurrentUser?.PlacedFurniture ?? new();

    // Guarda la disposición (server valida propiedad). Optimista: aplica local aunque el POST falle.
    public async Task<bool> SavePlacementsAsync(List<PlacedFurniture> placements)
    {
        if (CurrentUser == null) await InitializeAsync();
        if (CurrentUser == null) return false;
        CurrentUser.PlacedFurniture = placements;
        var (ok, _) = await PostAsync("/api/shop/placements", placements);
        return ok;
    }

    // Cuarto por defecto (mismo layout que el seed visual del RoomDiorama). Se "materializa" como colocaciones
    // reales la primera vez que el usuario compra/edita, para no perder cama/planta/gato al empezar a decorar.
    public List<PlacedFurniture> SeedPlacements()
    {
        var inv = CurrentUser?.Inventory ?? new();
        var seed = new List<PlacedFurniture>
        {
            new() { Name = "Cama Moderna", Sprite = "obj_bed_l", GridX = 3, GridY = 1, GridW = 2, GridD = 2 },
            new() { Name = "Planta",       Sprite = "obj_plant", GridX = 0, GridY = 4, GridW = 1, GridD = 1 },
            new() { Name = "Gato",         Sprite = "obj_cat_l", GridX = 5, GridY = 3, GridW = 1, GridD = 1 },
        };
        if (inv.ContainsKey("Lámpara de Pie"))
            seed.Add(new() { Name = "Lámpara de Pie", Sprite = "obj_lamp_l", GridX = 0, GridY = 0, GridW = 1, GridD = 1 });
        return seed;
    }

    // Al comprar un mueble (con sprite), lo coloca solo en la primera celda libre respetando el centro
    // (mascota). Devuelve false si el cuarto está lleno (el ítem queda en Inventory = "Guardados").
    public async Task<bool> AutoPlaceAsync(ShopItem item)
    {
        if (string.IsNullOrEmpty(item.SpriteId) || CurrentUser == null) return false;
        var current = CurrentUser.PlacedFurniture;
        // Primera compra: parte del cuarto por defecto para no perderlo.
        var placed = (current == null || current.Count == 0) ? SeedPlacements() : new List<PlacedFurniture>(current);
        var (w, d) = FootprintFor(item.SpriteId);
        var cell = FindFreeCell(placed, w, d);
        if (cell == null) return false; // sin espacio libre
        placed.Add(new PlacedFurniture { Name = item.Name, Sprite = item.SpriteId, GridX = cell.Value.x, GridY = cell.Value.y, GridW = w, GridD = d });
        await SavePlacementsAsync(placed);
        return true;
    }

    public static (int w, int d) FootprintFor(string sprite) =>
        sprite.Contains("bed") && !sprite.Contains("bedside") ? (2, 2) : (1, 1);

    // ¿Cabe un footprint WxD en (x,y) sin pisar otro mueble ni el centro (3,3) de la mascota?
    // `ignore` = el propio mueble cuando se está moviendo.
    public static bool CanPlace(IReadOnlyList<PlacedFurniture> placed, int x, int y, int w, int d, PlacedFurniture? ignore = null)
    {
        const int N = 6;
        if (x < 0 || y < 0 || x + w > N || y + d > N) return false;
        if (x <= 3 && 3 < x + w && y <= 3 && 3 < y + d) return false; // celda de la mascota
        foreach (var p in placed)
        {
            if (ReferenceEquals(p, ignore)) continue;
            if (x < p.GridX + p.GridW && p.GridX < x + w && y < p.GridY + p.GridD && p.GridY < y + d)
                return false;
        }
        return true;
    }

    // Busca la primera celda libre (6x6) para un footprint WxD, dejando el tile central (3,3) para la mascota.
    public static (int x, int y)? FindFreeCell(IReadOnlyList<PlacedFurniture> placed, int w, int d)
    {
        const int N = 6;
        for (int y = 0; y <= N - d; y++)
            for (int x = 0; x <= N - w; x++)
                if (CanPlace(placed, x, y, w, d)) return (x, y);
        return null;
    }

    // Equipa un estilo de habitación ya poseído (el server valida la propiedad). Refresca el estado.
    public async Task<(bool ok, string message)> EquipStyleAsync(string styleKey)
    {
        if (CurrentUser == null) await InitializeAsync();
        var (ok, error) = await PostAsync("/api/shop/equip", new { StyleKey = styleKey });
        if (ok) { await InitializeAsync(forceRefresh: true); return (true, string.Empty); }
        return (false, error ?? "No se pudo equipar el estilo.");
    }

    public Pet GetPet() => CurrentUser?.UserPet;
    public int GetGold() => CurrentUser?.UserPet?.GoldCoins ?? 0;
}
