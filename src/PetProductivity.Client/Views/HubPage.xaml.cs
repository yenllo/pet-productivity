using PetProductivity.Client.Services;
using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class HubPage : ContentPage
{
    private readonly HubViewModel _vm;

    public HubPage(HubViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // T31: el prompt de código aparecía en seco — pop de la tarjeta al abrir.
        _vm.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(HubViewModel.ShowJoinPrompt) && _vm.ShowJoinPrompt)
                await Anim.PopAsync(JoinCard);
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // No bloquear el render en la red: init en segundo plano (conecta realtime + carga familias).
        _ = _vm.InitializeAsync(); // refresca al volver del detalle / crear familia

        // T31-2: primera visita — qué son las familias.
        if (Onboarding.Pending("Hub")) { _vm.ShowOnboardCard(); Onboarding.MarkSeen("Hub"); }
    }
}
