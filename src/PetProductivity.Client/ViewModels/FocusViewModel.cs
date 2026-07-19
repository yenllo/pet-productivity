using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Shared;
using PetProductivity.Shared.Models;
using PetProductivity.Client.Services;
using Microsoft.Maui.Media;
using Microsoft.Maui.ApplicationModel;

namespace PetProductivity.Client.ViewModels;

[QueryProperty(nameof(PetId), "petId")]
[QueryProperty(nameof(PetName), "petName")]
[QueryProperty(nameof(PetImage), "petImage")]
[QueryProperty(nameof(Description), "description")]
[QueryProperty(nameof(GroupId), "groupId")]        // foco grupal: iniciar para este grupo
[QueryProperty(nameof(JoinGroupId), "joinGroupId")] // foco grupal: unirse al activo del grupo
public partial class FocusViewModel : ObservableObject
{
    [ObservableProperty] private string petId = string.Empty;
    [ObservableProperty] private string petName = string.Empty;
    [ObservableProperty] private string petImage = string.Empty;
    [ObservableProperty] private string description = string.Empty;
    [ObservableProperty] private string groupId = string.Empty;
    [ObservableProperty] private string joinGroupId = string.Empty;
    private bool _joinHandled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotFocusMode))]
    private bool isFocusMode;
    public bool IsNotFocusMode => !IsFocusMode;

    // Slider de duración: 5–120 min, pasos de 5.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FocusMinutesLabel))]
    private double focusMinutes = 25;
    public string FocusMinutesLabel => $"{(int)FocusMinutes} min";

    [ObservableProperty] private bool isCheckingProof;   // foto subida → Gemini la está revisando
    [ObservableProperty] private string focusRemaining = "00:00";
    [ObservableProperty] private string allowedAppsSummary = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string petImageSource = "pet_egg.png";

    // Meta diaria de foco.
    [ObservableProperty] private string dailyGoalLabel = string.Empty;
    [ObservableProperty] private double dailyGoalProgress;

    // Progreso 0..1 para el anillo (lo lee el SKCanvasView de la página al repintar).
    public double FocusProgress { get; private set; }

    // La página lo escucha para animar la mascota al completar (celebración).
    public event Action? Celebrate;

    // T31-2: tarjeta de primera visita (qué es el modo foco).
    [ObservableProperty] private bool showInfo;
    [ObservableProperty] private string infoTitle = string.Empty;
    [ObservableProperty] private string infoBody = string.Empty;

    public void ShowOnboardCard()
    {
        InfoTitle = L.T("Tu primer foco");
        InfoBody = L.T("Elige cuánto tiempo vas a concentrarte: durante la sesión solo podrás usar esta app y tus apps permitidas. Aguanta hasta el final y la recompensa es mayor — con foto de prueba a mitad, ×2.");
        ShowInfo = true;
    }

    private readonly GameDataService _game;
    private readonly FocusSessionService _focus;

    public FocusViewModel(GameDataService game, FocusSessionService focus)
    {
        _game = game;
        _focus = focus;
    }

    // La página llama esto en OnAppearing/OnDisappearing (refleja la sesión sin fugar handlers).
    public void AttachFocus()
    {
        ResolvePetImage();
        EnsurePetImage();        // tras restaurar (app reabierta), el usuario puede no estar cargado aún
        RefreshAllowedSummary();
        RefreshDailyGoal();
        IsFocusMode = _focus.IsActive;
        if (_focus.IsActive)
        {
            Description = _focus.Description; // tema real de la sesión en curso
            UpdateRemaining();
        }
        _focus.Tick += OnTick;
        _focus.Completed += OnEnded;
        _focus.Cancelled += OnCancelled;
        _focus.Midpoint += OnMidpoint;
        TryAutoJoin();

        // T27-L1 (#4): si el momento del comprobante pasó mientras NO estábamos en esta pantalla
        // (el prompt vive aquí), re-ofrecerlo al volver.
        if (_focus.IsActive && _focus.MidpointPending)
            OnMidpoint(this, EventArgs.Empty);
    }

    // Foco grupal: si llegamos con joinGroupId, únete al foco activo del grupo y arranca.
    private async void TryAutoJoin()
    {
        if (_joinHandled || _focus.IsActive) return;
        if (!Guid.TryParse(JoinGroupId, out var gid)) return;
        _joinHandled = true;
        try
        {
            var active = await _game.GetActiveGroupFocusAsync(gid);
            if (active is not { Active: true }) { StatusMessage = L.T("El foco grupal ya terminó."); return; }
            var info = await _game.JoinGroupFocusAsync(active.GroupFocusId);
            if (info == null) { StatusMessage = L.T("No se pudo unir al foco grupal."); return; }
            StartFromInfo(info);
        }
        catch (Exception ex) { StatusMessage = L.F("No se pudo unir al foco grupal: {0}", ex.Message); }
    }

    // Arranca la sesión local desde una sesión ya creada en el server (start/join grupal).
    private void StartFromInfo(GroupFocusInfo info)
    {
        var allowed = (Preferences.Get(FocusSessionService.AllowedAppsKey, "") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);
        var topic = string.IsNullOrWhiteSpace(info.Topic) ? L.T("Foco grupal") : info.Topic;
        if (!_focus.StartExisting(info.FocusSessionId, info.PetId, topic, info.StartedAt, info.TargetMinutes, allowed))
        {
            StatusMessage = L.T("Ya hay un foco activo.");
            return;
        }
        Description = _focus.Description;
        IsFocusMode = true;
        UpdateRemaining();
        StatusMessage = L.T("Foco grupal en marcha.");
    }

    public void DetachFocus()
    {
        _focus.Tick -= OnTick;
        _focus.Completed -= OnEnded;
        _focus.Cancelled -= OnCancelled;
        _focus.Midpoint -= OnMidpoint;
    }

    // A la mitad del foco: OFRECER (bonus opt-in) dejar un comprobante por 2× la recompensa. Saltar NO penaliza.
    private async void OnMidpoint(object? s, EventArgs e)
    {
        try
        {
            _focus.MidpointPending = false; // consumido: evita re-ofrecer al re-entrar a la pantalla
            if (!Preferences.Get(FocusSessionService.PhotoRewardsKey, true)) return; // desactivado en Ajustes

            // T14-C1b: consentimiento la PRIMERA vez — a dónde va la foto y cuánto se guarda.
            // Solo se marca al aceptar; si declina, se vuelve a preguntar en la próxima ocasión.
            if (!Preferences.Get("ProofConsentAccepted", false))
            {
                var consent = await Shell.Current.DisplayAlert(L.T("Foto de comprobante"),
                    L.T("La foto se envía a Google Gemini para verificarla con IA y se guarda 30 días en el servidor (después se borra sola). Es siempre opcional: saltarla no penaliza."),
                    L.T("Entendido"), L.T("Ahora no"));
                if (!consent) return;
                Preferences.Set("ProofConsentAccepted", true);
            }

            var mult = $"{Constants.PhotoBonusMultiplier:0.#}×";
            var pick = await Shell.Current.DisplayActionSheet(
                L.F("Demuestra que estás siendo productivo y gana {0} las recompensas: una selfie o una foto de lo que haces. (Desactívalo en Ajustes → Recompensas por foto)", mult),
                L.T("Ahora no"), null, L.T("📸 Tomar selfie"), L.T("🖼️ Subir foto"));

            if (pick != L.T("📸 Tomar selfie") && pick != L.T("🖼️ Subir foto")) return; // "Ahora no"/cancelar → sin penalización

            // La cámara/galería abre otra app: suspende el guardián para que NO cuente como "escape".
            FileResult? photo;
            _focus.Guard.Suspend();
            try
            {
                if (pick == L.T("📸 Tomar selfie"))
                {
                    var status = await Permissions.RequestAsync<Permissions.Camera>();
                    if (status != PermissionStatus.Granted) { StatusMessage = L.T("Sin permiso de cámara."); return; }
                    if (!MediaPicker.Default.IsCaptureSupported) { StatusMessage = L.T("Cámara no disponible."); return; }
                    photo = await MediaPicker.Default.CapturePhotoAsync();
                }
                else
                {
                    photo = await MediaPicker.Default.PickPhotoAsync();
                }
            }
            finally { _focus.Guard.Resume(); }

            if (photo == null) return; // el usuario canceló el picker → sin penalización

            // Indicador "en revisión" mientras Gemini juzga.
            IsCheckingProof = true;
            try
            {
                using var stream = await photo.OpenReadAsync();
                var verdict = await _game.UploadProofAsync(_focus.SessionId, _focus.Description, stream);
                StatusMessage = verdict == null ? L.T("Comprobante enviado.")
                    : verdict.Value ? L.F("¡Comprobante verificado! {0} recompensa al terminar.", mult)
                    : L.T("No se reconoció la foto; recompensa normal.");
            }
            finally { IsCheckingProof = false; }
        }
        catch (Exception ex) { StatusMessage = $"Comprobante: {ex.Message}"; }
    }

    // Si la mascota personal aún no cargó (restauración al reabrir), espera y re-resuelve el sprite.
    private async void EnsurePetImage()
    {
        if (!string.IsNullOrEmpty(PetImage) || _game.CurrentUser?.UserPet != null) return;
        try { await _game.InitializeAsync(); } catch { }
        ResolvePetImage();
    }

    private void ResolvePetImage()
    {
        // Grupo: imagen pasada por query. Personal: especie + etapa (helper, igual que el Dashboard).
        PetImageSource = !string.IsNullOrEmpty(PetImage)
            ? PetImage
            : PetVisuals.SpriteFor(_game.CurrentUser?.UserPet);
    }

    private void RefreshAllowedSummary()
    {
        if (!_focus.Guard.IsSupported) { AllowedAppsSummary = string.Empty; return; }
        var n = (Preferences.Get(FocusSessionService.AllowedAppsKey, "") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries).Length;
        AllowedAppsSummary = n == 0
            ? L.T("Sin apps permitidas (solo PetProductivity). Elige hasta 3 en Ajustes → Modo foco.")
            : L.F("{0} app(s) permitida(s). Cámbialas en Ajustes → Modo foco.", n);
    }

    private void RefreshDailyGoal()
    {
        var goal = FocusSessionService.DailyGoal();
        var today = FocusSessionService.TodayMinutes();
        DailyGoalLabel = L.F("Meta de hoy: {0} / {1} min", today, goal);
        DailyGoalProgress = goal > 0 ? Math.Clamp((double)today / goal, 0, 1) : 0;
    }

    partial void OnFocusMinutesChanged(double value)
    {
        var snapped = Math.Clamp(Math.Round(value / 5.0) * 5, 5, 120);
        if (Math.Abs(snapped - value) > 0.01) FocusMinutes = snapped;
    }

    private void OnTick(object? s, EventArgs e) => UpdateRemaining();

    private void UpdateRemaining()
    {
        var r = _focus.Remaining;
        if (r < TimeSpan.Zero) r = TimeSpan.Zero;
        FocusRemaining = $"{(int)r.TotalMinutes:00}:{r.Seconds:00}";
        FocusProgress = _focus.TargetMinutes > 0
            ? Math.Clamp(1 - r.TotalMinutes / _focus.TargetMinutes, 0, 1)
            : 0;
    }

    private void OnEnded(object? s, TaskResult result)
    {
        IsFocusMode = false;
        FocusProgress = 0;
        StatusMessage = result.Message;
        RefreshDailyGoal();
        Celebrate?.Invoke();
    }

    private void OnCancelled(object? s, EventArgs e)
    {
        IsFocusMode = false;
        FocusProgress = 0;
        StatusMessage = L.T("Foco cancelado. Sin recompensa.");
    }

    [RelayCommand]
    private async Task StartFocus()
    {
        if (string.IsNullOrWhiteSpace(Description))
        {
            StatusMessage = L.T("No hay una tarea para enfocar.");
            return;
        }

        var guard = _focus.Guard;
        if (guard.IsSupported && !guard.HasPermissions)
        {
            // Decir SOLO lo que falta de verdad: con uno de los dos ya concedido, el mensaje genérico
            // ("necesitas conceder acceso de uso Y mostrar sobre otras apps") sonaba a que no se había
            // guardado nada, aunque uno de los dos ya estuviera listo.
            var falta = new List<string>();
            if (!guard.HasUsageAccess) falta.Add($"'{L.T("Acceso de uso")}'");
            if (!guard.HasOverlay) falta.Add($"'{L.T("Mostrar sobre otras apps")}'");

            bool go = await Shell.Current.DisplayAlert(
                L.T("Permisos del modo foco"),
                L.F("Para bloquear otras apps falta conceder {0}. Lo haces en Ajustes → Modo foco.", string.Join(L.T(" y "), falta)),
                L.T("Ir a Ajustes"), L.T("Ahora no"));
            if (go) await Shell.Current.GoToAsync("//App/SettingsPage");
            return;
        }

        // Foco grupal: el server crea la sesión compartida y avisa a la familia.
        if (Guid.TryParse(GroupId, out var grp))
        {
            var info = await _game.StartGroupFocusAsync(grp, (int)FocusMinutes, Description);
            if (info == null) { StatusMessage = L.T("No se pudo iniciar el foco grupal."); return; }
            StartFromInfo(info);
            return;
        }

        var allowed = (Preferences.Get(FocusSessionService.AllowedAppsKey, "") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);
        var targetPet = Guid.TryParse(PetId, out var g) ? g : Guid.Empty;

        var ok = await _focus.StartAsync(targetPet, Description, (int)FocusMinutes, allowed);
        if (!ok) { StatusMessage = L.T("No se pudo iniciar el foco."); return; }

        IsFocusMode = true;
        UpdateRemaining();
        StatusMessage = guard.IsSupported
            ? L.T("En foco. Quédate en PetProductivity o en tus apps permitidas.")
            : L.T("En foco. Mantén la app abierta.");
    }

    [RelayCommand]
    private async Task CancelFocus()
    {
        // Incentivo: lo que ganaría si aguanta (estimación base; el server es la verdad).
        int difficulty = Math.Clamp((int)Math.Ceiling(_focus.TargetMinutes / 15.0), 1, 10);
        int xp = difficulty * 10, gold = difficulty * 5;

        bool confirmCancel = await Shell.Current.DisplayAlert(
            L.T("¿Cancelar el foco?"),
            L.F("Si aguantas hasta el final ganarás ~{0} XP y ~{1} Oro.", xp, gold) + "\n" + L.T("Si cancelas ahora no recibes nada."),
            L.T("Sí, cancelar"), L.T("Seguir concentrado"));

        if (confirmCancel) await _focus.CancelAsync();
    }
}
