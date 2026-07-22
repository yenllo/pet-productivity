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
		// Arrastre directo: el mueble sigue al dedo con imán a celda (el D-pad queda para ajuste fino).
		SandboxCanvas.DragStarted += (gx, gy) =>
		{
			viewModel.OnDragStarted(gx, gy);
			SandboxCanvas.Highlight = viewModel.SelectedCell;
			SandboxCanvas.InvalidateSurface();
		};
		SandboxCanvas.DragMoved += (gx, gy) =>
		{
			viewModel.OnDragMoved(gx, gy);
			SandboxCanvas.Highlight = viewModel.SelectedCell;
			SandboxCanvas.InvalidateSurface();
		};
		SandboxCanvas.DragEnded += viewModel.OnDragEnded;
		// T5-D: al subir TotalXp, la mascota salta (~1.2 s) usando el mismo reloj del diorama.
		viewModel.CelebrateXp += () => RoomCanvas.Celebrate();
		// Rechazo de movimiento en el sandbox → rombo rojo fugaz en la celda destino.
		viewModel.InvalidMove += (x, y, w, d) => SandboxCanvas.FlashInvalid(x, y, w, d);
		// T31-5: celebración del ritual — pop de la celda marcada y destello de la línea ganadora.
		viewModel.RitualCellPopped += async i =>
		{
			if (i < RitualGrid.Children.Count && RitualGrid.Children[i] is Border cell)
			{
				cell.Scale = 0.8;
				await cell.ScaleTo(1.08, 140, Easing.CubicOut);
				await cell.ScaleTo(1, 110, Easing.CubicIn);
			}
		};
		viewModel.RitualLineCompleted += async line =>
		{
			try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
			foreach (var i in line)
			{
				if (i < RitualGrid.Children.Count && RitualGrid.Children[i] is Border cell)
				{
					var c = cell; // captura por iteración (el fade encadenado corre en segundo plano)
					_ = c.FadeTo(0.35, 90).ContinueWith(_ =>
						MainThread.BeginInvokeOnMainThread(() => _ = c.FadeTo(1, 260)));
					await Task.Delay(80);
				}
			}
			await Services.Anim.PopAsync(XpChip); // el chip ×1.2 entra con pop
		};
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
			// T31: los overlays de estado y renombrar hábito aparecían en seco — pop.
			else if (e.PropertyName == nameof(DashboardViewModel.ShowStatusPicker) && viewModel.ShowStatusPicker)
				await Services.Anim.PopAsync(StatusCard);
			else if (e.PropertyName == nameof(DashboardViewModel.ShowRitualPrompt) && viewModel.ShowRitualPrompt)
				await Services.Anim.PopAsync(RitualPromptCard);
			// T31-2: cada tarjeta del onboarding entra con pop (al abrir y al pasar de paso).
			else if (viewModel.ShowOnboarding &&
			         (e.PropertyName == nameof(DashboardViewModel.ShowOnboarding) ||
			          e.PropertyName == nameof(DashboardViewModel.OnboardingStep)))
				await Services.Anim.PopAsync(OnboardCard);
			// T31-6: la evolución era el hito más importante y entraba estática — flash + spring.
			else if (e.PropertyName == nameof(DashboardViewModel.ShowEvolution) && viewModel.ShowEvolution)
			{
				EvolutionOverlay.Opacity = 0;
				EvolutionPet.Scale = 0.2;
				await EvolutionOverlay.FadeTo(1, 220, Easing.CubicOut);
				_ = EvolutionFlash.FadeTo(0.85, 140).ContinueWith(_ =>
					MainThread.BeginInvokeOnMainThread(() => _ = EvolutionFlash.FadeTo(0, 420)));
				try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
				await EvolutionPet.ScaleTo(1.18, 420, Easing.SpringOut);
				await EvolutionPet.ScaleTo(1, 180, Easing.CubicIn);
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
		if (!_entered)
		{
			_entered = true;
			await RevealAsync();
		}

		// T31-2: tutorial de primera vez. Fuera del guard _entered para que "¿Cómo se juega?"
		// (Ajustes resetea las flags y navega aquí) lo reabra aunque la página ya haya vivido.
		if (Services.Onboarding.Pending("Dashboard") && BindingContext is DashboardViewModel vm2)
			vm2.StartOnboarding();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		RoomCanvas.StopAnimation();
		SandboxCanvas.StopAnimation(); // por si salen de la pestaña con el sandbox abierto
	}

	// La mascota ya no es un overlay de XAML: respira y salta DENTRO del lienzo (RoomDiorama), que es
	// donde vive su sombra — por eso ahora se apoya en el suelo en vez de flotar. Aquí solo se guarda
	// el reloj del diorama para el resto de animaciones de la página.
	float _lastT;

	void OnFrameTick(float t) => _lastT = t;

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
