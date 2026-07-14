namespace PetProductivity.Client.Services;

public interface INotificationService
{
    void ShowNotification(string title, string message);
    void ScheduleNotification(string title, string message, DateTime time);
}

public class NotificationService : INotificationService
{
    public void ShowNotification(string title, string message)
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
