using PetProductivity.Client.ViewModels;

namespace PetProductivity.Client.Views;

[QueryProperty(nameof(GroupId), "groupId")]
public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;
    public string GroupId { get; set; } = string.Empty;

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _vm = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.GroupId = Guid.TryParse(GroupId, out var g) ? g : null;
        Title = _vm.GroupId == null ? "Historial" : "Historial del grupo";
        await _vm.LoadAsync();
    }
}
