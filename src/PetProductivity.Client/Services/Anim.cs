namespace PetProductivity.Client.Services;

/// <summary>
/// Helper único de animación (T31). Factoriza el pop de la celebración de TaskPage y añade
/// fade, número flotante y contador; los contadores animan la propiedad del VM (nunca el
/// Label.Text directo, que rompería el binding).
/// </summary>
public static class Anim
{
    /// <summary>Pop de aparición de tarjeta: Scale 0.7→1.06→1 + Fade 0→1.</summary>
    public static async Task PopAsync(VisualElement v)
    {
        v.Scale = 0.7;
        v.Opacity = 0;
        await Task.WhenAll(v.FadeTo(1, 150), v.ScaleTo(1.06, 180, Easing.CubicOut));
        await v.ScaleTo(1, 120, Easing.CubicIn);
    }

    /// <summary>Fade de entrada para fondos/overlays que hoy aparecen en seco.</summary>
    public static Task FadeInAsync(VisualElement v, uint ms = 180)
    {
        v.Opacity = 0;
        return v.FadeTo(1, ms, Easing.CubicOut);
    }

    /// <summary>Número flotante (+XP/+Oro): aparece, sube y se desvanece; deja la vista oculta y en su sitio.</summary>
    public static async Task FloatUpAsync(View v, double dy = -70, uint ms = 800)
    {
        v.Opacity = 0;
        v.TranslationY = 0;
        v.IsVisible = true;
        await v.FadeTo(1, 120);
        await Task.WhenAll(v.TranslateTo(0, dy, ms, Easing.CubicOut), v.FadeTo(0, ms, Easing.CubicIn));
        v.IsVisible = false;
        v.TranslationY = 0;
    }

    /// <summary>Contador animado: llama a set() con valores intermedios hasta llegar a to.</summary>
    public static async Task CountAsync(int from, int to, Action<int> set, uint ms = 600)
    {
        int steps = Math.Clamp(Math.Abs(to - from), 1, 20);
        for (int i = 1; i <= steps; i++)
        {
            set(from + (to - from) * i / steps);
            await Task.Delay((int)(ms / (uint)steps));
        }
    }
}
