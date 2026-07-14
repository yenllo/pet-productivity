using Microsoft.Maui.Controls.Shapes;

namespace PetProductivity.Client.Controls;

/// <summary>
/// Barra de progreso con extremos redondeados (la ProgressBar de MAUI no admite corner radius).
/// Track redondeado + fill redondeado anclado a la izquierda; el ancho del fill = ancho × Progress.
/// </summary>
public class RoundedBar : ContentView
{
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(RoundedBar), 0.0, propertyChanged: OnVisualChanged);
    public static readonly BindableProperty FillProperty =
        BindableProperty.Create(nameof(Fill), typeof(Color), typeof(RoundedBar), Colors.HotPink, propertyChanged: OnFillChanged);
    public static readonly BindableProperty TrackProperty =
        BindableProperty.Create(nameof(Track), typeof(Color), typeof(RoundedBar), Color.FromArgb("#33FFFFFF"), propertyChanged: OnTrackChanged);
    public static readonly BindableProperty BarHeightProperty =
        BindableProperty.Create(nameof(BarHeight), typeof(double), typeof(RoundedBar), 6.0, propertyChanged: OnVisualChanged);

    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public Color Fill { get => (Color)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Color Track { get => (Color)GetValue(TrackProperty); set => SetValue(TrackProperty, value); }
    public double BarHeight { get => (double)GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    private readonly Border _track;
    private readonly Border _fill;

    public RoundedBar()
    {
        _fill = new Border { StrokeThickness = 0, HorizontalOptions = LayoutOptions.Start, WidthRequest = 0, StrokeShape = new RoundRectangle { CornerRadius = 99 } };
        _track = new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 99 }, Content = _fill };
        Content = _track;
        Apply();
        _track.SizeChanged += (_, _) => UpdateFill();
    }

    private void Apply()
    {
        _track.HeightRequest = BarHeight;
        _fill.HeightRequest = BarHeight;
        _track.BackgroundColor = Track;
        _fill.BackgroundColor = Fill;
        UpdateFill();
    }

    private void UpdateFill()
    {
        var w = _track.Width;
        _fill.WidthRequest = w <= 0 ? 0 : w * Math.Clamp(Progress, 0, 1);
    }

    private static void OnVisualChanged(BindableObject b, object o, object n) => ((RoundedBar)b).Apply();
    private static void OnFillChanged(BindableObject b, object o, object n) => ((RoundedBar)b)._fill.BackgroundColor = (Color)n;
    private static void OnTrackChanged(BindableObject b, object o, object n) => ((RoundedBar)b)._track.BackgroundColor = (Color)n;
}
