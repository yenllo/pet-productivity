using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PetProductivity.Client.ViewModels
{
    public partial class IntroViewModel : ObservableObject
    {
        [RelayCommand]
        private async Task NavigateToDashboard()
        {
            // Navigate to the LoginPage
            // Using absolute route to ensure we handle the stack correctly
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
