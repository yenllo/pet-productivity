namespace PetProductivity.Client.Services;

// T27-L3 (#25): "claro suave" — solo superficies, tarjetas y tinta; el arte (cuarto, sprites,
// glows) queda igual. Los overrides se ponen como entradas DIRECTAS de Application.Resources,
// que tienen prioridad sobre los diccionarios mergeados (Colors.xaml). Quitar = volver a oscuro.
// Los estilos del design system usan DynamicResource para estas claves; las páginas usan
// StaticResource y se re-resuelven al reconstruir el AppShell (lo hace el toggle).
public static class ThemeService
{
    private static readonly Dictionary<string, object> Light = new()
    {
        // Superficies
        ["Stage"] = Color.FromArgb("#FFF3F0FA"),
        ["MokoBg1"] = Color.FromArgb("#FFEFEBF8"),
        ["MokoBg2"] = Color.FromArgb("#FFE9E3F5"),
        ["MokoBg3"] = Color.FromArgb("#FFE1DAF1"),
        ["Glass"] = Color.FromArgb("#0D221A3F"),
        ["Glass2"] = Color.FromArgb("#16221A3F"),
        ["StrokeSoft"] = Color.FromArgb("#1F221A3F"),
        ["StrokeHard"] = Color.FromArgb("#30221A3F"),
        // T31: velo modal — 35% de tinta sobre fondo claro (el 55% oscuro aplanaba todo en gris).
        ["Scrim"] = Color.FromArgb("#59221A3F"),
        // Tinta (oscura sobre claro)
        ["Ink"] = Color.FromArgb("#FF221A3F"),
        ["Ink2"] = Color.FromArgb("#FF453C6B"),
        ["Ink3"] = Color.FromArgb("#FF6B6390"),
        ["Ink4"] = Color.FromArgb("#FF8D86B0"),
    };

    public static void Apply(bool dark)
    {
        var res = Application.Current!.Resources;

        if (dark)
        {
            foreach (var key in Light.Keys) res.Remove(key);
            res.Remove("AppBgBrush"); res.Remove("CardGlassBrush"); res.Remove("VignetteBrush");
            return;
        }

        foreach (var (key, value) in Light) res[key] = value;

        // Los brushes se recrean en cada Apply (instancias nuevas; baratas).
        res["AppBgBrush"] = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.08),
            Radius = 1.15,
            GradientStops =
            {
                new GradientStop(Color.FromArgb("#FFFFFFFF"), 0.0f),
                new GradientStop(Color.FromArgb("#FFF1EDFA"), 0.5f),
                new GradientStop(Color.FromArgb("#FFE3DDF2"), 1.0f),
            }
        };
        res["CardGlassBrush"] = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(Color.FromArgb("#CCFFFFFF"), 0.0f),
                new GradientStop(Color.FromArgb("#99FFFFFF"), 1.0f),
            },
            new Point(0, 0), new Point(0, 1));
        res["VignetteBrush"] = new RadialGradientBrush
        {
            Center = new Point(0.5, 0.4),
            Radius = 0.72,
            GradientStops =
            {
                new GradientStop(Color.FromArgb("#00000000"), 0.45f),
                new GradientStop(Color.FromArgb("#1F000000"), 1.0f),
            }
        };
    }
}
