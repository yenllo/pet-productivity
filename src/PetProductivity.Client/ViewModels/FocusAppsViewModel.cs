using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Client.Services;
using System.Collections.ObjectModel;

namespace PetProductivity.Client.ViewModels;

public partial class SelectableApp : ObservableObject
{
    public FocusApp Info { get; }
    public string Label => Info.Label;
    public ImageSource? Icon => Info.Icon;
    [ObservableProperty] private bool isSelected;
    public SelectableApp(FocusApp info, bool selected) { Info = info; isSelected = selected; }
}

/// Picker de apps permitidas durante el foco (máx 3). Guarda los paquetes en Preferences.
public partial class FocusAppsViewModel : ObservableObject
{
    private readonly IFocusGuard _guard;
    public ObservableCollection<SelectableApp> Apps { get; } = new();

    [ObservableProperty] private string header = "Cargando apps…";

    public FocusAppsViewModel(IFocusGuard guard)
    {
        _guard = guard;
        _ = LoadAsync();
    }

    // Carga apps + iconos en un hilo de fondo (convertir cada icono a PNG es pesado → evita el ANR).
    private async Task LoadAsync()
    {
        var selected = (Preferences.Get(FocusSessionService.AllowedAppsKey, "") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var apps = await Task.Run(() => _guard.GetLaunchableApps());
        foreach (var a in apps)
            Apps.Add(new SelectableApp(a, selected.Contains(a.Package)));
        UpdateHeader();
    }

    [RelayCommand]
    private void Toggle(SelectableApp? app)
    {
        if (app == null) return;
        if (!app.IsSelected && Apps.Count(a => a.IsSelected) >= 3) return; // máx 3
        app.IsSelected = !app.IsSelected;
        Save();
        UpdateHeader();
    }

    private void Save() =>
        Preferences.Set(FocusSessionService.AllowedAppsKey,
            string.Join(",", Apps.Where(a => a.IsSelected).Select(a => a.Info.Package)));

    private void UpdateHeader() => Header = $"Elegidas: {Apps.Count(a => a.IsSelected)}/3";
}
