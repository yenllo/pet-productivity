using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Devices;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.Services;

/// <summary>
/// Sesión de foco viva (AC3 v2). Es singleton para SOBREVIVIR la navegación entre páginas: la cuenta
/// atrás y el bloqueo de apps no dependen de que la pantalla de registrar tarea siga abierta. Al llegar
/// a 0 completa (recompensa por tiempo cumplido); cancelar = 0. El server sigue siendo la verdad.
/// </summary>
public class FocusSessionService
{
    public const string AllowedAppsKey = "FocusAllowedApps";
    public const string EscapesKey = "FocusEscapes";
    public const string GoalKey = "FocusGoalMinutes";
    public const string PhotoRewardsKey = "PhotoRewardsEnabled"; // switch del comprobante (default ON)
    private const string TodayDateKey = "FocusTodayDate";
    private const string TodayMinutesKey = "FocusTodayMinutes";
    // Persistencia de la sesión activa (sobrevive a que cierren la app desde recientes).
    private const string ActiveSidKey = "FocusActiveSid";
    private const string ActiveStartKey = "FocusActiveStart";
    private const string ActiveTargetKey = "FocusActiveTarget";
    private const string ActivePetKey = "FocusActivePet";
    private const string ActiveDescKey = "FocusActiveDesc";

    private readonly GameDataService _game;
    private readonly IFocusGuard _guard;
    private readonly INotificationService _notify;
    private IDispatcherTimer? _timer;

    public FocusSessionService(GameDataService game, IFocusGuard guard, INotificationService notify)
    {
        _game = game;
        _guard = guard;
        _notify = notify;
    }

    public IFocusGuard Guard => _guard;
    public bool IsActive { get; private set; }
    public Guid SessionId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public int TargetMinutes { get; private set; }
    public DateTime StartUtc { get; private set; }

    public TimeSpan Remaining =>
        IsActive ? TimeSpan.FromMinutes(TargetMinutes) - (DateTime.UtcNow - StartUtc) : TimeSpan.Zero;

    public event EventHandler? Tick;
    public event EventHandler<TaskResult>? Completed;
    public event EventHandler? Cancelled;
    public event EventHandler? Midpoint;   // a la mitad del foco → pedir comprobante
    private bool _midpointFired;
    // T27-L1 (#4): el prompt del comprobante vive en la pantalla de foco. Si el usuario NO está ahí
    // cuando llega la mitad, esto queda en true y una notificación lo avisa; al volver, se re-ofrece.
    public bool MidpointPending { get; set; }

    public async Task<bool> StartAsync(Guid petId, string description, int minutes, IEnumerable<string> allowed)
    {
        if (IsActive) return false;

        var id = await _game.StartFocusAsync(petId, minutes);
        if (id == null) return false;

        SessionId = id.Value;
        Description = description;
        TargetMinutes = minutes;
        StartUtc = DateTime.UtcNow;
        IsActive = true;

        Preferences.Set(EscapesKey, 0); // reinicia el contador de escapes de esta sesión
        _midpointFired = false;
        Persist(petId);

        _guard.Cancelled += OnGuardCancelled;
        try { _guard.Start(allowed, minutes); } catch { /* el bloqueo es best-effort */ }

        StartTimer();
        return true;
    }

    // Foco grupal: arranca desde una sesión que el server ya creó (StartedAt compartido), sin re-crearla.
    public bool StartExisting(Guid sessionId, Guid petId, string description, DateTime startUtc, int minutes, IEnumerable<string> allowed)
    {
        if (IsActive) return false;

        SessionId = sessionId;
        Description = description;
        TargetMinutes = minutes;
        StartUtc = startUtc;
        IsActive = true;

        Preferences.Set(EscapesKey, 0);
        var remaining = TimeSpan.FromMinutes(minutes) - (DateTime.UtcNow - startUtc);
        _midpointFired = remaining.TotalMinutes <= minutes / 2.0;
        Persist(petId);

        _guard.Cancelled += OnGuardCancelled;
        try { _guard.Start(allowed, (int)Math.Ceiling(Math.Max(1, remaining.TotalMinutes))); } catch { }

        StartTimer();
        return true;
    }

    private void StartTimer()
    {
        _timer = Application.Current!.Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void Persist(Guid petId)
    {
        Preferences.Set(ActiveSidKey, SessionId.ToString());
        Preferences.Set(ActiveStartKey, StartUtc.Ticks.ToString());
        Preferences.Set(ActiveTargetKey, TargetMinutes);
        Preferences.Set(ActivePetKey, petId.ToString());
        Preferences.Set(ActiveDescKey, Description);
    }

    private static void ClearPersisted()
    {
        foreach (var k in new[] { ActiveSidKey, ActiveStartKey, ActiveTargetKey, ActivePetKey, ActiveDescKey })
            Preferences.Remove(k);
    }

    // Al abrir la app, reanuda un foco que seguía vivo (p. ej. tras cerrar desde recientes).
    // Devuelve true si hay que llevar al usuario a la pantalla de foco.
    public bool TryRestore()
    {
        if (IsActive) return true;
        var sidStr = Preferences.Get(ActiveSidKey, "");
        if (!Guid.TryParse(sidStr, out var sid)) return false;

        var target = Preferences.Get(ActiveTargetKey, 0);
        long.TryParse(Preferences.Get(ActiveStartKey, ""), out var startTicks);
        if (startTicks == 0 || target <= 0) { ClearPersisted(); return false; }

        SessionId = sid;
        TargetMinutes = target;
        StartUtc = new DateTime(startTicks, DateTimeKind.Utc);
        Description = Preferences.Get(ActiveDescKey, "");
        IsActive = true;

        var remaining = TimeSpan.FromMinutes(target) - (DateTime.UtcNow - StartUtc);
        if (remaining <= TimeSpan.Zero) { _ = CompleteAsync(); return false; } // ya cumplió → completar
        _midpointFired = remaining.TotalMinutes <= target / 2.0; // si ya pasó la mitad, no re-disparar

        var allowed = (Preferences.Get(AllowedAppsKey, "") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        _guard.Cancelled += OnGuardCancelled;
        try { _guard.Start(allowed, (int)Math.Ceiling(remaining.TotalMinutes)); } catch { }
        StartTimer();
        return true;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Tick?.Invoke(this, EventArgs.Empty);
        // A mitad de sesión: dispara el comprobante una sola vez.
        if (!_midpointFired && Remaining.TotalMinutes <= TargetMinutes / 2.0)
        {
            _midpointFired = true;
            MidpointPending = true;
            Midpoint?.Invoke(this, EventArgs.Empty); // si estás en la pantalla de foco, el prompt sale ya
            // T27-L1 (#4): si NO estás en esa pantalla, el prompt se perdía. Ahora te avisa por notificación.
            if (Preferences.Get(PhotoRewardsKey, true))
                try { _notify.ShowNotification(L.T("📸 Comprobante disponible"),
                    L.T("Vuelve a PetProductivity y demuestra tu foco para ganar 2× la recompensa."), openFocus: true); } catch { }
        }
        if (Remaining <= TimeSpan.Zero) _ = CompleteAsync();
    }

    public async Task CompleteAsync()
    {
        if (!IsActive) return;
        IsActive = false;
        var served = TargetMinutes;
        StopInternals();

        var result = await _game.CompleteFocusAsync(SessionId, Description);

        AddDailyMinutes(served);
        var escapes = Preferences.Get(EscapesKey, 0);

        var msg = result.XpEarned > 0
            ? L.F("¡Foco completado! +{0} XP y +{1} Oro.", result.XpEarned, result.GoldEarned)
            : result.Message;
        if (escapes > 0) msg += $" (saliste {escapes} {(escapes == 1 ? "vez" : "veces")})";
        result.Message = msg;

        try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(450)); } catch { }
        try { _notify.ShowNotification("Modo foco", msg); } catch { }

        Completed?.Invoke(this, result);
    }

    // Suma minutos al acumulado del día (para la meta diaria), reseteando al cambiar de fecha.
    private static void AddDailyMinutes(int minutes)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        if (Preferences.Get(TodayDateKey, "") != today)
        {
            Preferences.Set(TodayDateKey, today);
            Preferences.Set(TodayMinutesKey, 0);
        }
        Preferences.Set(TodayMinutesKey, Preferences.Get(TodayMinutesKey, 0) + minutes);
    }

    // Minutos enfocados hoy (0 si la fecha guardada no es hoy).
    public static int TodayMinutes() =>
        Preferences.Get(TodayDateKey, "") == DateTime.Now.ToString("yyyy-MM-dd")
            ? Preferences.Get(TodayMinutesKey, 0) : 0;

    public static int DailyGoal() => Preferences.Get(GoalKey, 60);

    public async Task CancelAsync()
    {
        if (!IsActive) return;
        IsActive = false;
        StopInternals();
        await _game.CancelFocusAsync(SessionId);
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void OnGuardCancelled(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(async () => await CancelAsync());

    private void StopInternals()
    {
        _timer?.Stop();
        _timer = null;
        MidpointPending = false;
        _guard.Cancelled -= OnGuardCancelled;
        try { _guard.Stop(); } catch { }
        ClearPersisted();
    }
}
