namespace PetProductivity.Client.Views;

using PetProductivity.Client.ViewModels;

public partial class RegisterPage : ContentPage
{
	public RegisterPage(RegisterViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
