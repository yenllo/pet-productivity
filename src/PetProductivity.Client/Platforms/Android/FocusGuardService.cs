#if ANDROID
using Android.App;
using Android.App.Usage;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Display;
using Android.OS;
using Android.Provider;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Storage;
using Color = Android.Graphics.Color;
using Format = Android.Graphics.Format;
using Button = Android.Widget.Button;
using TextView = Android.Widget.TextView;
using LinearLayout = Android.Widget.LinearLayout;
using AView = Android.Views.View;

namespace PetProductivity.Client.Platforms.Android;

/// <summary>
/// Servicio en primer plano del modo foco (AC3 v2). Sondea en un HILO DE FONDO qué app está al frente
/// (UsageStatsManager); si no está en la lista blanca (PetProductivity + apps elegidas + launcher +
/// Ajustes + systemui), muestra un OVERLAY a pantalla completa ("vuelve a tu foco"), en vez de cambiar
/// de app de golpe. No actúa con la pantalla apagada o bloqueada. Notificación con cuenta atrás nativa.
/// </summary>
[Service(Name = "yenllo.org.PetProductivity.FocusGuardService", Exported = false)]
public class FocusGuardService : Service
{
    public static event EventHandler? CancelRequested;
    // Pausa la vigilancia sin parar el servicio (la cámara del comprobante NO debe contar como escape).
    public static volatile bool Suspended;

    public const string ActionCancel = "yenllo.org.PetProductivity.FOCUS_CANCEL";
    private const string ChannelId = "focus_guard";
    private const int NotifId = 4242;

    private readonly HashSet<string> _allowed = new();
    private long _endTime;
    private string _self = string.Empty;
    private UsageStatsManager? _usm;
    private PowerManager? _power;
    private KeyguardManager? _keyguard;
    private HandlerThread? _thread;
    private Handler? _bg;
    private Handler? _main;
    private global::Java.Lang.Runnable? _poll;
    private bool _wasBlocked;

    private IWindowManager? _wm;
    private AView? _overlay;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        // Reinicio del sistema sin extras (tras matar la app): no levantar un guardián "zombie".
        if (intent == null) { StopSelf(); return StartCommandResult.NotSticky; }

        if (intent.Action == ActionCancel)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        Suspended = false; // arranque limpio
        _self = ApplicationContext!.PackageName!;
        _allowed.Clear();
        _allowed.Add(_self);
        _allowed.Add("com.android.systemui");
        AddSystemPackages();
        var arr = intent?.GetStringArrayExtra("allowed");
        if (arr != null)
            foreach (var p in arr)
                if (!string.IsNullOrEmpty(p)) _allowed.Add(p);
        _endTime = intent?.GetLongExtra("endTime", 0L) ?? 0L;

        _usm = (UsageStatsManager?)GetSystemService(UsageStatsService);
        _power = (PowerManager?)GetSystemService(PowerService);
        _keyguard = (KeyguardManager?)GetSystemService(KeyguardService);
        _main = new Handler(Looper.MainLooper!);

        StartForegroundCompat();
        StartPolling();
        return StartCommandResult.NotSticky; // tras kill no auto-reinicia (la app lo reanuda al abrir)
    }

    // Launcher y Ajustes siempre permitidos (poder ir a inicio / configuración sin que aparezca el overlay).
    private void AddSystemPackages()
    {
        var pm = PackageManager;
        if (pm == null) return;

        var home = new Intent(Intent.ActionMain);
        home.AddCategory(Intent.CategoryHome);
        var launcher = pm.ResolveActivity(home, PackageInfoFlags.MatchDefaultOnly)?.ActivityInfo?.PackageName;
        if (!string.IsNullOrEmpty(launcher)) _allowed.Add(launcher);

        var settings = pm.ResolveActivity(new Intent(Settings.ActionSettings), PackageInfoFlags.MatchDefaultOnly)?.ActivityInfo?.PackageName;
        if (!string.IsNullOrEmpty(settings)) _allowed.Add(settings);
        _allowed.Add("com.android.settings");
    }

    private void StartForegroundCompat()
    {
        var notif = BuildNotification();
        if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
            StartForeground(NotifId, notif, global::Android.Content.PM.ForegroundService.TypeSpecialUse);
        else
            StartForeground(NotifId, notif);
    }

    private void StartPolling()
    {
        _thread = new HandlerThread("focus-guard");
        _thread.Start();
        _bg = new Handler(_thread.Looper!);
        _poll = new global::Java.Lang.Runnable(() =>
        {
            var now = global::Java.Lang.JavaSystem.CurrentTimeMillis();
            if (_endTime > 0 && now >= _endTime) { _main?.Post(() => StopSelf()); return; }

            bool screenOn = _power?.IsInteractive ?? true;
            bool locked = _keyguard?.IsKeyguardLocked ?? false;
            var fg = CurrentForegroundApp();
            bool blocked = !string.IsNullOrEmpty(fg) && !_allowed.Contains(fg!);
            bool shouldBlock = screenOn && !locked && blocked && !Suspended;

            if (shouldBlock && !_wasBlocked)
            {
                Preferences.Set("FocusEscapes", Preferences.Get("FocusEscapes", 0) + 1);
                _main?.Post(ShowOverlay);
            }
            else if (!shouldBlock && _wasBlocked)
            {
                _main?.Post(HideOverlay);
            }
            _wasBlocked = shouldBlock;

            _bg!.PostDelayed(_poll!, 1000);
        });
        _bg.Post(_poll);
    }

    private string? CurrentForegroundApp()
    {
        if (_usm == null) return null;
        var now = global::Java.Lang.JavaSystem.CurrentTimeMillis();
        var events = _usm.QueryEvents(now - 6000, now);
        if (events == null) return null;

        var ev = new UsageEvents.Event();
        string? pkg = null;
        while (events.HasNextEvent)
        {
            events.GetNextEvent(ev);
            if (ev.EventType == UsageEventType.MoveToForeground)
                pkg = ev.PackageName;
        }
        return pkg;
    }

    // ---- Overlay (en el hilo principal) ----
    private Context OverlayContext()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            var dm = (DisplayManager?)GetSystemService(DisplayService);
            var display = dm?.GetDisplay(global::Android.Views.Display.DefaultDisplay);
            if (display != null)
            {
                var winCtx = CreateDisplayContext(display).CreateWindowContext((int)WindowManagerTypes.ApplicationOverlay, null);
                if (winCtx != null) return winCtx;
            }
        }
        return this;
    }

    private void ShowOverlay()
    {
        if (_overlay != null) return;
        var ctx = OverlayContext();
        _wm = ctx.GetSystemService(WindowService).JavaCast<IWindowManager>();
        if (_wm == null) return;

        var layout = new LinearLayout(ctx) { Orientation = global::Android.Widget.Orientation.Vertical };
        layout.SetBackgroundColor(Color.Argb(242, 14, 10, 28));
        layout.SetGravity(GravityFlags.Center);
        layout.SetPadding(64, 64, 64, 64);

        var title = new TextView(ctx) { Text = "🔒 En foco", TextSize = 26, Gravity = GravityFlags.Center };
        title.SetTextColor(Color.White);
        var msg = new TextView(ctx) { Text = "Vuelve a PetProductivity para seguir concentrado.", TextSize = 15, Gravity = GravityFlags.Center };
        msg.SetTextColor(Color.Argb(220, 232, 226, 248));
        msg.SetPadding(0, 24, 0, 40);

        var back = new Button(ctx) { Text = "Volver a PetProductivity" };
        back.Click += (s, e) => LaunchApp();
        var cancel = new Button(ctx) { Text = "Cancelar foco" };
        cancel.Click += (s, e) =>
        {
            var i = new Intent(this, typeof(FocusGuardService));
            i.SetAction(ActionCancel);
            StartService(i);
        };

        layout.AddView(title);
        layout.AddView(msg);
        layout.AddView(back);
        layout.AddView(cancel);

        var type = Build.VERSION.SdkInt >= BuildVersionCodes.O ? WindowManagerTypes.ApplicationOverlay : WindowManagerTypes.Phone;
        var lp = new WindowManagerLayoutParams
        {
            Width = ViewGroup.LayoutParams.MatchParent,
            Height = ViewGroup.LayoutParams.MatchParent,
            Type = type,
            Flags = WindowManagerFlags.NotFocusable | WindowManagerFlags.LayoutInScreen,
            Format = Format.Translucent,
            Gravity = GravityFlags.Top | GravityFlags.Start
        };

        try { _wm.AddView(layout, lp); _overlay = layout; }
        catch { _overlay = null; }
    }

    private void HideOverlay()
    {
        if (_overlay == null) return;
        try { _wm?.RemoveView(_overlay); } catch { }
        _overlay = null;
    }

    private void LaunchApp()
    {
        HideOverlay();
        var i = PackageManager?.GetLaunchIntentForPackage(_self);
        i?.AddFlags(ActivityFlags.NewTask | ActivityFlags.ReorderToFront);
        if (i != null) StartActivity(i);
    }

    private Notification BuildNotification()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var nm = (NotificationManager?)GetSystemService(NotificationService);
            nm?.CreateNotificationChannel(new NotificationChannel(ChannelId, "Modo foco", NotificationImportance.Low));
        }

        var launch = PackageManager?.GetLaunchIntentForPackage(_self);
        var contentPi = PendingIntent.GetActivity(this, 0, launch,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var cancelIntent = new Intent(this, typeof(FocusGuardService));
        cancelIntent.SetAction(ActionCancel);
        var cancelPi = PendingIntent.GetService(this, 1, cancelIntent,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new Notification.Builder(this, ChannelId)
            .SetContentTitle("Modo foco activo")
            .SetContentText("Mantente concentrado")
            .SetSmallIcon(global::Android.Resource.Drawable.IcLockLock)
            .SetOngoing(true)
            .SetUsesChronometer(true)
            .SetWhen(_endTime)
            .AddAction(global::Android.Resource.Drawable.IcMenuCloseClearCancel, "Cancelar foco", cancelPi);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.N) builder.SetChronometerCountDown(true);
        if (contentPi != null) builder.SetContentIntent(contentPi);
        return builder.Build();
    }

    public override void OnDestroy()
    {
        _bg?.RemoveCallbacksAndMessages(null);
        _thread?.QuitSafely();
        HideOverlay();
        base.OnDestroy();
    }
}
#endif
