using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class HubPage : ContentPage
{
    private readonly HubViewModel _vm;

    public HubPage(HubViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // No bloquear el render en la red: init en segundo plano (conecta realtime + carga familias).
        _ = _vm.InitializeAsync(); // refresca al volver del detalle / crear familia
    }
}
