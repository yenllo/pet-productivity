namespace PetProductivity.Client.Views;

using PetProductivity.Client.ViewModels;

public partial class ShopPage : ContentPage
{
	public ShopPage(ShopViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // No bloquear el render en la red: los datos llegan en segundo plano y los bindings actualizan.
        if (BindingContext is ShopViewModel vm)
        {
            _ = vm.InitializeAsync();
            // T31-2: primera visita — cómo funciona la tienda.
            if (Services.Onboarding.Pending("Shop")) { vm.ShowOnboardCard(); Services.Onboarding.MarkSeen("Shop"); }
        }
    }
}
