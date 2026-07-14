using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PetProductivity.Client.ViewModels;

public partial class RitualCellViewModel : ObservableObject
{
    [ObservableProperty]
    private int index;

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private string label;

    private readonly Func<int, Task> _toggleAction;

    public RitualCellViewModel(int index, bool isCompleted, string label, Func<int, Task> toggleAction)
    {
        Index = index;
        IsCompleted = isCompleted;
        Label = label;
        _toggleAction = toggleAction;
    }

    [RelayCommand]
    public async Task Toggle()
    {
        // T7: el flip especulativo vive en el padre (que sabe si estamos renombrando o toggleando).
        await _toggleAction(Index);
    }
}
