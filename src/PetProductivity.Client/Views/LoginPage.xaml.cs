namespace PetProductivity.Client.Views;

using PetProductivity.Client.ViewModels;

public partial class LoginPage : ContentPage
{
	public LoginPage(LoginViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
