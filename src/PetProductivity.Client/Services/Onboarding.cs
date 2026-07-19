namespace PetProductivity.Client.Services;

/// <summary>
/// T31-2: primera vez por pantalla. Flags en Preferences (Onboard_Dashboard/Hub/Shop/Focus).
/// Se muestra a todos una vez: detectar "usuario nuevo" es frágil y el contenido nadie lo ha
/// visto explicado. Logout/borrar cuenta NO las limpian a propósito (el usuario ya aprendió);
/// "¿Cómo se juega?" en Ajustes es la vía de replay.
/// </summary>
public static class Onboarding
{
    private static readonly string[] Pages = { "Dashboard", "Hub", "Shop", "Focus" };

    public static bool Pending(string page) => !Preferences.Get($"Onboard_{page}", false);
    public static void MarkSeen(string page) => Preferences.Set($"Onboard_{page}", true);

    public static void ResetAll()
    {
        foreach (var p in Pages) Preferences.Remove($"Onboard_{p}");
    }
}
