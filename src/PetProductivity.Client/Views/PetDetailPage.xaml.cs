using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class PetDetailPage : ContentPage
{
    private readonly PetDetailViewModel _vm;

    public PetDetailPage(PetDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        RoomCanvas.FrameTick += OnFrameTick;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RoomCanvas.StartAnimation();
        await _vm.InitializeAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        RoomCanvas.StopAnimation();
        _vm.Cleanup(); // desuscribe los eventos de tiempo real
    }

    // El diorama corre un solo timer; aquí respira la mascota y pulsa el banner de Frenesí.
    void OnFrameTick(float t)
    {
        // T31: un evento ×2 llamado "frenesí" no puede quedarse quieto.
        if (_vm.IsFrenzyActive)
        {
            FrenzyBanner.Scale = 1 + 0.015 * Math.Sin(t * 2 * Math.PI / 1.3);
            FrenzyBanner.Opacity = 0.86 + 0.14 * Math.Sin(t * 2 * Math.PI / 1.3);
        }
        if (!_vm.IsHatched) return;
        GroupPet.Scale = 1 + 0.03 * Math.Sin(t * 2 * Math.PI / 3.2);
        GroupPet.TranslationY = 4 * Math.Sin(t * 2 * Math.PI / 3.8);
    }
}
