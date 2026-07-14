namespace PetProductivity.Client.Controls;

public partial class BackButton : ContentView
{
    public BackButton() => InitializeComponent();

    private async void OnClicked(object sender, EventArgs e)
    {
        // ponytail: navega "arriba" en el stack del Shell; try/catch como el resto del cliente por si el stack está vacío.
        try { await Shell.Current.GoToAsync(".."); } catch { }
    }
}
