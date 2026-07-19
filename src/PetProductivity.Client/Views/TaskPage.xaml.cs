using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class TaskPage : ContentPage
{
	private readonly TaskViewModel _vm;

	public TaskPage(TaskViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _vm = viewModel;

		// #20: "pop" de la tarjeta de celebración al aparecer (T31: factorizado en Anim)
		// + números flotantes de +XP/+Oro (T31-4).
		_vm.PropertyChanged += async (_, e) =>
		{
			if (e.PropertyName == nameof(TaskViewModel.ShowCelebration) && _vm.ShowCelebration)
			{
				await Services.Anim.PopAsync(CelebCard);
				FloatXp.Text = _vm.CelebXp;
				FloatGold.Text = _vm.CelebGold;
				_ = Services.Anim.FloatUpAsync(FloatXp);
				await Task.Delay(120);
				_ = Services.Anim.FloatUpAsync(FloatGold);
			}
		};
	}

	// Al volver (p. ej. tras un foco) refresca el registro para incluir la tarea recién hecha.
	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await _vm.RefreshFeedAsync();
	}
}
