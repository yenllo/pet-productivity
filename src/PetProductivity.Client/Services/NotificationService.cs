namespace PetProductivity.Client.Services;

public interface INotificationService
{
    // openFocus: al tocar la notificación con la app ya viva, reabre FocusPage (mismo mecanismo que la
    // notificación del guardián de foco) en vez de solo traer la app al frente donde se hubiera quedado.
    void ShowNotification(string title, string message, bool openFocus = false);
    void ScheduleNotification(string title, string message, DateTime time);
}

public class NotificationService : INotificationService
{
    public void ShowNotification(string title, string message, bool openFocus = false)
    {
        // Placeholder for native implementation or Plugin.LocalNotification
        // For now, we just log it. In a real app, this would use Android NotificationManager.
        System.Diagnostics.Debug.WriteLine($"[NOTIFICATION] {title}: {message}");
    }

    public void ScheduleNotification(string title, string message, DateTime time)
    {
        System.Diagnostics.Debug.WriteLine($"[SCHEDULED] {title}: {message} at {time}");
    }
}
