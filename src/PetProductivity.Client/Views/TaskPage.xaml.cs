using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class TaskPage : ContentPage
{
	private readonly TaskViewModel _vm;

	public TaskPage(TaskViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _vm = viewModel;

		// #20: "pop" de la tarjeta de celebración al aparecer.
		_vm.PropertyChanged += async (_, e) =>
		{
			if (e.PropertyName == nameof(TaskViewModel.ShowCelebration) && _vm.ShowCelebration)
			{
				CelebCard.Scale = 0.7;
				CelebCard.Opacity = 0;
				await Task.WhenAll(
					CelebCard.FadeTo(1, 150),
					CelebCard.ScaleTo(1.06, 180, Easing.CubicOut));
				await CelebCard.ScaleTo(1, 120, Easing.CubicIn);
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
