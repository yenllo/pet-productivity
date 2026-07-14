namespace PetProductivity.Client.Views;

public partial class SettingsPage : ContentPage
{
	private readonly ViewModels.SettingsViewModel _vm;

	public SettingsPage(ViewModels.SettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _vm = viewModel;
	}

	// Al volver (p. ej. tras conceder permisos o elegir apps) refresca el estado del modo foco.
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = _vm.RefreshFocus();
	}
}
