using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class FocusAppsPage : ContentPage
{
    public FocusAppsPage(FocusAppsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
