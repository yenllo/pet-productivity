using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Client.Services;
using PetProductivity.Shared.Models;

namespace PetProductivity.Client.ViewModels;

public partial class CreateGroupViewModel : ObservableObject
{
    private readonly GroupService _groups;

    [ObservableProperty] private string groupName = string.Empty;
    [ObservableProperty] private int selectedArchetypeIndex;
    [ObservableProperty] private double maxMembers = 4;

    // Índices alineados con IndexToArchetype. Neutral queda excluido (es solo personal).
    // L.T(...): sin esto, con el idioma en inglés esta lista se veía en español en medio de una
    // pantalla ya traducida (encontrado navegando "Nueva familia" en el emulador).
    public List<string> Archetypes { get; } = new()
    {
        L.T("Estudio"), L.T("Tecnología"), L.T("Creativo"), L.T("Atlético"), L.T("Ejecutivo"), L.T("Hogar (pareja)"), L.T("Gremio")
    };

    public CreateGroupViewModel(GroupService groups) => _groups = groups;

    private static Archetype IndexToArchetype(int i) => i switch
    {
        0 => Archetype.Scholar,
        1 => Archetype.Technologist,
        2 => Archetype.Creator,
        3 => Archetype.Athlete,
        4 => Archetype.Executive,
        5 => Archetype.Household,
        _ => Archetype.Guild
    };

    [RelayCommand]
    private async Task Create()
    {
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            await Shell.Current.DisplayAlert(L.T("Falta el nombre"), L.T("Ponle un nombre a tu familia."), "OK");
            return;
        }

        var (ok, msg) = await _groups.CreateGroupAsync(
            GroupName.Trim(), IndexToArchetype(SelectedArchetypeIndex), (int)MaxMembers);

        await Shell.Current.DisplayAlert(ok ? L.T("Listo") : L.T("No se pudo"), msg, "OK");
        if (ok) await Shell.Current.GoToAsync("..");
    }
}
