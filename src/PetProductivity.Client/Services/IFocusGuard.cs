namespace PetProductivity.Client.Services;

public record FocusApp(string Package, string Label, Microsoft.Maui.Controls.ImageSource? Icon = null);

/// <summary>
/// Guardián de foco por plataforma (AC3 v2). En Android vigila qué app está al frente durante el foco
/// y, si no está en la lista blanca (PetProductivity + las apps elegidas), superpone una pantalla que
/// invita a volver. En iOS/Windows es no-op (bloqueo real iOS = Family Controls, fase posterior).
/// </summary>
public interface IFocusGuard
{
    bool IsSupported { get; }
    bool HasPermissions { get; }          // uso + superponer (ambos)
    bool HasUsageAccess { get; }
    bool HasOverlay { get; }
    void RequestUsageAccess();            // abre Ajustes → Acceso de uso
    void RequestOverlay();                // abre Ajustes → Mostrar sobre otras apps
    List<FocusApp> GetLaunchableApps();
    void Start(IEnumerable<string> allowedPackages, int minutes);
    void Stop();
    void Suspend();                       // pausa la vigilancia (p. ej. al abrir la cámara del comprobante)
    void Resume();                        // reanuda la vigilancia
    event EventHandler? Cancelled;        // el usuario tocó "Cancelar foco" en la notificación
    // Se toca la notificación/overlay de foco con la app YA VIVA (no arranque en frío): a diferencia
    // del restore de App.xaml.cs (que solo corre al crear la App), esto cubre volver a FocusPage
    // cuando el usuario navegó al menú principal sin cerrar la app.
    event EventHandler? ReopenRequested;
}

/// No-op para plataformas sin bloqueo (iOS/Windows/Mac). El foco corre, pero sin bloquear apps.
public class NoopFocusGuard : IFocusGuard
{
    public bool IsSupported => false;
    public bool HasPermissions => true;   // nada que conceder → no estorba el inicio del foco
    public bool HasUsageAccess => true;
    public bool HasOverlay => true;
    public void RequestUsageAccess() { }
    public void RequestOverlay() { }
    public List<FocusApp> GetLaunchableApps() => new();
    public void Start(IEnumerable<string> allowedPackages, int minutes) { }
    public void Stop() { }
    public void Suspend() { }
    public void Resume() { }
    public event EventHandler? Cancelled { add { } remove { } }
    public event EventHandler? ReopenRequested { add { } remove { } }
}
