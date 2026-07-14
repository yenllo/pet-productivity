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

    // El diorama corre un solo timer; aquí solo respira el overlay de la mascota (cuando ya nació).
    void OnFrameTick(float t)
    {
        if (!_vm.IsHatched) return;
        GroupPet.Scale = 1 + 0.03 * Math.Sin(t * 2 * Math.PI / 3.2);
        GroupPet.TranslationY = 4 * Math.Sin(t * 2 * Math.PI / 3.8);
    }
}
