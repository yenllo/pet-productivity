using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

public partial class CreateGroupPage : ContentPage
{
    public CreateGroupPage(CreateGroupViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
