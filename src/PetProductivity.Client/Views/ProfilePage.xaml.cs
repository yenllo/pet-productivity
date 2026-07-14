namespace PetProductivity.Client.Views;

using PetProductivity.Client.ViewModels;

public partial class ProfilePage : ContentPage
{
	public ProfilePage(ProfileViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

    bool _entered;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // No bloquear el render en la red: init en segundo plano, fade de inmediato.
        if (BindingContext is ProfileViewModel vm)
            _ = vm.InitializeAsync();
        if (_entered) return;   // T27-L1: el fade solo en la 1ª entrada (no parpadear en cada visita)
        _entered = true;
        this.Opacity = 0;
        await this.FadeTo(1, 500, Easing.CubicOut);
    }
}
