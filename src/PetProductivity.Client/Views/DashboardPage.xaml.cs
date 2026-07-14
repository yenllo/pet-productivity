using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class DashboardPage : ContentPage
{
	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
		RoomCanvas.FrameTick += OnFrameTick;
		SandboxCanvas.CellTapped += OnCellTapped;
		// T5-D: al subir TotalXp, la mascota salta (~1.2 s) usando el mismo reloj del diorama.
		viewModel.CelebrateXp += () => _celebrateUntil = _lastT + 1.2f;
		viewModel.PropertyChanged += async (_, e) =>
		{
			// La selección vive en el VM (taps, pad, listas); aquí solo se refleja el resalte.
			if (e.PropertyName == nameof(DashboardViewModel.SelectedCell))
			{
				SandboxCanvas.Highlight = viewModel.SelectedCell;
				SandboxCanvas.InvalidateSurface();
			}
			else if (e.PropertyName == nameof(DashboardViewModel.EditMode))
			{
				SandboxCanvas.Highlight = viewModel.SelectedCell;
				if (viewModel.EditMode) { SandboxCanvas.StartAnimation(); await ImmerseAsync(); }
				else SandboxCanvas.StopAnimation();
			}
		};
	}

	// Inmersión: el overlay aparece en fade y el diorama "entra" con zoom (Scale 0.65 → 1).
	async Task ImmerseAsync()
	{
		SandboxOverlay.Opacity = 0;
		SandboxCanvas.Scale = 0.65;
		_ = SandboxOverlay.FadeTo(1, 220, Easing.CubicOut);
		await SandboxCanvas.ScaleTo(1, 320, Easing.CubicOut);
	}

	// El diorama sandbox avisa qué celda de piso tocó el usuario → el VM decide, y aquí
	// reflejamos la selección (resalte) en el control.
	void OnCellTapped(int gx, int gy)
	{
		if (BindingContext is not DashboardViewModel vm) return;
		vm.OnCellTapped(gx, gy);
		SandboxCanvas.Highlight = vm.SelectedCell;
		SandboxCanvas.InvalidateSurface();
	}

	bool _entered;
	protected override async void OnAppearing()
	{
		base.OnAppearing();

		// Pinta y anima de inmediato; los datos del server llegan en segundo plano y los bindings
		// (CurrentPet, IsLoading) actualizan la UI solos. No bloquear el primer render en la red.
		if (BindingContext is DashboardViewModel vm)
			_ = vm.InitializeAsync();

		RoomCanvas.StartAnimation();

		// T27-L1: la entrada solo anima la PRIMERA vez. Antes se hacía RevealAsync y LUEGO
		// this.Opacity=0 + FadeTo(1) → toda la página parpadeaba a invisible y reaparecía (el "flash"),
		// y se repetía en cada visita a la pestaña. Ahora la reveal corre una vez y sin parpadeo.
		if (_entered) return;
		_entered = true;
		await RevealAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		RoomCanvas.StopAnimation();
		SandboxCanvas.StopAnimation(); // por si salen de la pestaña con el sandbox abierto
	}

	// El diorama corre un solo timer (en RoomDiorama). Aquí solo respira/flota el overlay de la mascota.
	float _lastT;
	float _celebrateUntil = -1;

	void OnFrameTick(float t)
	{
		_lastT = t;
		double breathe = 1 + 0.03 * Math.Sin(t * 2 * Math.PI / 3.2);

		// T5-D: 3 saltitos que se apagan (celebración de XP); 0 fuera de la ventana.
		float hop = 0;
		if (t < _celebrateUntil)
		{
			float p = (_celebrateUntil - t) / 1.2f; // 1 → 0
			hop = (float)(Math.Abs(Math.Sin((1 - p) * Math.PI * 3)) * 18 * p);
		}

		MokoImage.Scale = breathe;
		MokoImage.TranslationY = -8 + 4 * Math.Sin(t * 2 * Math.PI / 3.8) - hop;
		// La burbuja flota suave junto a la mascota.
		MoodBubble.TranslationY = -56 + 3 * Math.Sin(t * 2 * Math.PI / 3.8);
	}

	async Task RevealAsync()
	{
		if (ContentStack == null) return;
		foreach (var child in ContentStack.Children.OfType<VisualElement>())
		{
			child.Opacity = 0;
			child.TranslationY = 22;
		}
		await Task.Delay(60);
		foreach (var child in ContentStack.Children.OfType<VisualElement>())
		{
			_ = child.FadeTo(1, 420, Easing.CubicOut);
			_ = child.TranslateTo(0, 0, 420, Easing.CubicOut);
			await Task.Delay(70);
		}
	}
}
