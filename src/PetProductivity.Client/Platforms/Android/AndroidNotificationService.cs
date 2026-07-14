#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using PetProductivity.Client.Services;
using AndroidApp = Android.App.Application;

namespace PetProductivity.Client.Platforms.Android;

/// <summary>Notificación local real en Android (NotificationManager + canal). Reemplaza el stub.</summary>
public class AndroidNotificationService : INotificationService
{
    private const string ChannelId = "pet_general";
    private static int _next = 1000;

    public void ShowNotification(string title, string message)
    {
        var ctx = AndroidApp.Context;
        var mgr = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);
        if (mgr == null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            mgr.CreateNotificationChannel(new NotificationChannel(ChannelId, "PetProductivity", NotificationImportance.Default));

        var launch = ctx.PackageManager?.GetLaunchIntentForPackage(ctx.PackageName!);
        var pi = PendingIntent.GetActivity(ctx, 0, launch,
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new Notification.Builder(ctx, ChannelId)
            .SetContentTitle(title)
            .SetContentText(message)
            .SetStyle(new Notification.BigTextStyle().BigText(message))
            .SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetAutoCancel(true);
        if (pi != null) builder.SetContentIntent(pi);

        mgr.Notify(_next++, builder.Build());
    }

    // No hay programación diferida por ahora; muestra de inmediato (suficiente para los avisos actuales).
    public void ScheduleNotification(string title, string message, DateTime time) => ShowNotification(title, message);
}
#endif
