using PetProductivity.Shared.Models;

namespace PetProductivity.Server.Services;

/// <summary>
/// T2-D: política anti-spam de push, obligatoria para TODO aviso — el primer push molesto quema el
/// canal para siempre. Reglas: respetar NotificationsEnabled y token, quiet hours locales (23:00–08:00,
/// vía LocalDay/T8) y máximo 1 push por TIPO por día (User.LastNotifications).
/// </summary>
public static class NotificationPolicy
{
    public const int QuietStartHour = 23; // desde las 23:00 locales
    public const int QuietEndHour = 8;    // hasta las 08:00 locales

    public static bool ShouldSend(User u, string type, DateTime? utcNow = null)
    {
        if (!u.NotificationsEnabled || string.IsNullOrEmpty(u.DeviceToken)) return false;

        var local = TimeZoneInfo.ConvertTimeFromUtc(utcNow ?? DateTime.UtcNow, LocalDay.ZoneFor(u));
        if (local.Hour >= QuietStartHour || local.Hour < QuietEndHour) return false;

        var today = LocalDay.TodayTokenFor(u, utcNow);
        return !(u.LastNotifications.TryGetValue(type, out var last) && last.Date == today.Date);
    }

    public static void MarkSent(User u, string type, DateTime? utcNow = null) =>
        u.LastNotifications[type] = LocalDay.TodayTokenFor(u, utcNow);
}
