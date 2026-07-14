#if ANDROID
using Android.App;
using Android.App.Usage;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Provider;
using PetProductivity.Client.Services;
using Application = Android.App.Application;

namespace PetProductivity.Client.Platforms.Android;

/// <summary>
/// Guardián de foco en Android: lista apps lanzables, gestiona los permisos especiales (acceso de uso
/// + superponer) y arranca/detiene <see cref="FocusGuardService"/>, que vigila la app al frente.
/// </summary>
public class FocusGuard : IFocusGuard
{
    public event EventHandler? Cancelled;

    public FocusGuard()
    {
        // "Cancelar foco" en el overlay → reenvía al evento de instancia (la sesión lo escucha).
        FocusGuardService.CancelRequested += (s, e) => Cancelled?.Invoke(this, EventArgs.Empty);
    }

    public bool IsSupported => true;

    public bool HasUsageAccess => UsageAccessGranted();
    public bool HasOverlay => Settings.CanDrawOverlays(Application.Context);
    public bool HasPermissions => HasUsageAccess && HasOverlay;

    public void RequestUsageAccess()
    {
        var i = new Intent(Settings.ActionUsageAccessSettings);
        i.AddFlags(ActivityFlags.NewTask);
        Application.Context.StartActivity(i);
    }

    public void RequestOverlay()
    {
        var ctx = Application.Context;
        var i = new Intent(Settings.ActionManageOverlayPermission,
            global::Android.Net.Uri.Parse("package:" + ctx.PackageName));
        i.AddFlags(ActivityFlags.NewTask);
        ctx.StartActivity(i);
    }

    public List<FocusApp> GetLaunchableApps()
    {
        var ctx = Application.Context;
        var pm = ctx.PackageManager!;
        var intent = new Intent(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);

        var result = new Dictionary<string, FocusApp>();
        foreach (var ri in pm.QueryIntentActivities(intent, 0))
        {
            var pkg = ri.ActivityInfo?.PackageName;
            if (string.IsNullOrEmpty(pkg) || pkg == ctx.PackageName || result.ContainsKey(pkg)) continue;
            var label = ri.LoadLabel(pm) ?? pkg;
            result[pkg] = new FocusApp(pkg, label, IconFor(ri, pm));
        }
        return result.Values.OrderBy(a => a.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // ponytail: icono a 96px → PNG en memoria; carga sincrónica, ok para el picker (decenas de apps).
    private static Microsoft.Maui.Controls.ImageSource? IconFor(ResolveInfo ri, PackageManager pm)
    {
        try
        {
            var d = ri.LoadIcon(pm);
            if (d == null) return null;
            using var bmp = ToBitmap(d, 96);
            using var ms = new MemoryStream();
            bmp.Compress(Bitmap.CompressFormat.Png!, 100, ms);
            var bytes = ms.ToArray();
            return Microsoft.Maui.Controls.ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch { return null; }
    }

    private static Bitmap ToBitmap(Drawable d, int size)
    {
        if (d is BitmapDrawable bd && bd.Bitmap != null)
            return Bitmap.CreateScaledBitmap(bd.Bitmap, size, size, true)!;
        // Iconos adaptativos / vectoriales: dibujar sobre un bitmap del tamaño pedido.
        var bmp = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!)!;
        using var canvas = new Canvas(bmp);
        d.SetBounds(0, 0, size, size);
        d.Draw(canvas);
        return bmp;
    }

    public void Start(IEnumerable<string> allowedPackages, int minutes)
    {
        var ctx = Application.Context;
        var i = new Intent(ctx, typeof(FocusGuardService));
        i.PutExtra("allowed", allowedPackages.ToArray());
        i.PutExtra("endTime", Java.Lang.JavaSystem.CurrentTimeMillis() + (long)minutes * 60_000L);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            ctx.StartForegroundService(i);
        else
            ctx.StartService(i);
    }

    public void Stop()
    {
        var ctx = Application.Context;
        ctx.StopService(new Intent(ctx, typeof(FocusGuardService)));
    }

    public void Suspend() => FocusGuardService.Suspended = true;
    public void Resume() => FocusGuardService.Suspended = false;

    private static bool UsageAccessGranted()
    {
        var ctx = Application.Context;
        var appOps = (AppOpsManager?)ctx.GetSystemService(Context.AppOpsService);
        if (appOps == null) return false;

        AppOpsManagerMode mode;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
            mode = appOps.UnsafeCheckOpNoThrow(AppOpsManager.OpstrGetUsageStats!, global::Android.OS.Process.MyUid(), ctx.PackageName!);
        else
#pragma warning disable CS0618
            mode = appOps.CheckOpNoThrow(AppOpsManager.OpstrGetUsageStats!, global::Android.OS.Process.MyUid(), ctx.PackageName!);
#pragma warning restore CS0618
        return mode == AppOpsManagerMode.Allowed;
    }
}
#endif
