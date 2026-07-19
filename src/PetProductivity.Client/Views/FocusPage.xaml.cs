using PetProductivity.Client.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using System.ComponentModel;

namespace PetProductivity.Client.Views;

public partial class FocusPage : ContentPage
{
    private readonly FocusViewModel _vm;

    public FocusPage(FocusViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AttachFocus();
        _vm.PropertyChanged += OnVmPropertyChanged;
        _vm.Celebrate += OnCelebrate;
        RingCanvas.InvalidateSurface();
        ApplyFocusLock();
        StartAnim();

        // T31-2: primera visita — qué es el foco. Se marca vista aunque entre en plena sesión
        // (la tarjeta se cierra con un tap y no bloquea el temporizador).
        if (Services.Onboarding.Pending("Focus")) { _vm.ShowOnboardCard(); Services.Onboarding.MarkSeen("Focus"); }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopAnim();
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _vm.Celebrate -= OnCelebrate;
        _vm.DetachFocus();
        Shell.SetTabBarIsVisible(this, true); // restaurar al salir
        Shell.SetNavBarIsVisible(this, true);
    }

    // Mientras el foco está activo: ocultar TabBar y NavBar (flecha atrás) → no se sale sin cancelar.
    private void ApplyFocusLock()
    {
        Shell.SetTabBarIsVisible(this, !_vm.IsFocusMode);
        Shell.SetNavBarIsVisible(this, !_vm.IsFocusMode);
    }

    // La mascota NO se queda quieta: bob suave + motas de concentración (se ve "trabajando").
    private IDispatcherTimer? _anim;
    private float _t;

    private void StartAnim()
    {
        if (_anim == null)
        {
            _anim = Dispatcher.CreateTimer();
            _anim.Interval = TimeSpan.FromMilliseconds(40);
            _anim.Tick += (_, _) =>
            {
                _t += 0.04f;
                PetImage.TranslationY = 3 * Math.Sin(_t * 2 * Math.PI / 2.5);
                RingCanvas.InvalidateSurface();
            };
        }
        _anim.Start();
    }

    private void StopAnim() => _anim?.Stop();

    // Botón "atrás" del sistema durante el foco → muestra el diálogo de cancelar (no sale directo).
    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsFocusMode)
        {
            if (_vm.CancelFocusCommand.CanExecute(null)) _vm.CancelFocusCommand.Execute(null);
            return true; // consume el back
        }
        return base.OnBackButtonPressed();
    }

    // "Pop" de la mascota al completar el foco (celebración).
    private async void OnCelebrate()
    {
        RingCanvas.InvalidateSurface();
        try
        {
            await PetImage.ScaleTo(1.25, 160, Easing.CubicOut);
            await PetImage.ScaleTo(1.0, 160, Easing.CubicIn);
        }
        catch { }
    }

    // El temporizador avanza cada segundo (FocusRemaining) → repinta el anillo.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(FocusViewModel.FocusRemaining) or nameof(FocusViewModel.IsFocusMode))
            RingCanvas.InvalidateSurface();
        if (e.PropertyName == nameof(FocusViewModel.IsFocusMode))
            ApplyFocusLock(); // ocultar/restaurar TabBar al iniciar/terminar el foco
    }

    // Anillo: pista tenue + arco de progreso (rosa→magenta) + motas de concentración junto a la mascota.
    private void OnPaintRing(object sender, SKPaintSurfaceEventArgs e)
    {
        var info = e.Info;
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        float cx = info.Width / 2f, cy = info.Height / 2f;
        float radius = Math.Min(cx, cy) - 16f;
        const float stroke = 14f;

        using (var track = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = stroke,
            StrokeCap = SKStrokeCap.Round,
            Color = new SKColor(255, 255, 255, 30)
        })
            canvas.DrawCircle(cx, cy, radius, track);

        double progress = _vm.FocusProgress;
        if (_vm.IsFocusMode && progress > 0)
        {
            var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
            using var arc = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = stroke,
                StrokeCap = SKStrokeCap.Round,
                Shader = SKShader.CreateSweepGradient(
                    new SKPoint(cx, cy),
                    new[] { new SKColor(0xFF, 0x5F, 0x8F), new SKColor(0xB9, 0x6B, 0xFF), new SKColor(0xFF, 0x5F, 0x8F) },
                    null)
            };
            using var path = new SKPath();
            path.AddArc(rect, -90, (float)(360 * progress));
            canvas.DrawPath(path, arc);
        }

        // Motas de concentración subiendo junto a la mascota (se ve "trabajando", no quieta)
        using var mote = new SKPaint { IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2f) };
        for (int k = 0; k < 8; k++)
        {
            float prog = (_t * 0.25f + k * 0.13f) % 1f;
            float mx = cx + (float)Math.Sin(k * 0.9f + _t * 0.7f) * radius * 0.42f;
            float my = cy + radius * 0.5f - prog * radius;
            byte a = (byte)(150 * (1 - prog) * (0.4f + 0.6f * (0.5f + 0.5f * (float)Math.Sin(_t * 2 + k))));
            mote.Color = new SKColor(0xFF, 0x9F, 0xC4, a);
            canvas.DrawCircle(mx, my, 2.2f + 1.2f * (float)Math.Sin(_t + k), mote);
        }
    }
}
