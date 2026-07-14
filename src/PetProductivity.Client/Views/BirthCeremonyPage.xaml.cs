using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class BirthCeremonyPage : ContentPage
{
    // The 6 extracted PNG frames from the original egg_crack.gif
    private static readonly string[] EggFrames = new[]
    {
        "egg_frame_0.png", // intact egg
        "egg_frame_1.png", // tiny wobble
        "egg_frame_2.png", // first crack appears
        "egg_frame_3.png", // crack widens
        "egg_frame_4.png", // big crack
        "egg_frame_5.png", // almost hatching
    };

    // Duration (ms) to hold each frame — progressive acceleration
    // Total ≈ 4200ms before flash
    private static readonly int[] FrameDurations = new[]
    {
        1200,  // Frame 0: egg sits still for a moment
         800,  // Frame 1: first subtle wobble
         700,  // Frame 2: crack appears — tension builds
         600,  // Frame 3: crack widens — accelerating
         500,  // Frame 4: big crack — faster
         400,  // Frame 5: almost open — fastest
    };

    public BirthCeremonyPage(BirthCeremonyViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        this.Loaded += BirthCeremonyPage_Loaded;
    }

    private async void BirthCeremonyPage_Loaded(object? sender, EventArgs e)
    {
        this.Loaded -= BirthCeremonyPage_Loaded;

        try
        {
            // Small initial delay so user sees the egg before anything happens
            await Task.Delay(600);

            // Play each frame with controlled timing + wobble effect
            for (int i = 0; i < EggFrames.Length; i++)
            {
                EggImage.Source = EggFrames[i];

                // Add a shake/wobble animation for frames 1+ (the egg is cracking)
                if (i > 0)
                {
                    await WobbleEgg(intensity: 3 + (i * 2)); // increasing intensity
                }

                await Task.Delay(FrameDurations[i]);
            }

            // Brief dramatic pause after last frame
            await Task.Delay(200);

            // Flash in (blinding white)
            await FlashOverlay.FadeTo(1.0, 300, Easing.CubicOut);

            // Switch images behind the flash
            EggImage.IsVisible = false;
            CreatureImage.Opacity = 1;

            // Flash out
            await FlashOverlay.FadeTo(0, 500, Easing.CubicIn);

            // Fade in inputs
            await InputContainer.FadeTo(1, 600, Easing.CubicOut);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BirthCeremony] Animation error: {ex.Message}");
            // Fallback: just show the creature immediately
            EggImage.IsVisible = false;
            CreatureImage.Opacity = 1;
            InputContainer.Opacity = 1;
        }
    }

    /// <summary>
    /// Quick left-right wobble to simulate the egg shaking as it cracks.
    /// </summary>
    private async Task WobbleEgg(double intensity)
    {
        uint speed = 60; // ms per wobble step
        await EggImage.TranslateTo(-intensity, 0, speed);
        await EggImage.TranslateTo(intensity, 0, speed);
        await EggImage.TranslateTo(-intensity * 0.6, 0, speed);
        await EggImage.TranslateTo(intensity * 0.6, 0, speed);
        await EggImage.TranslateTo(0, 0, speed);
    }
}
