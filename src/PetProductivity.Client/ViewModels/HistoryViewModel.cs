using CommunityToolkit.Mvvm.ComponentModel;
using PetProductivity.Client.Services;
using System.Collections.ObjectModel;

namespace PetProductivity.Client.ViewModels;

public partial class HistoryEntry : ObservableObject
{
    public string Description { get; set; } = string.Empty;
    public string DateText { get; set; } = string.Empty;
    public string RewardText { get; set; } = string.Empty;
    public string Who { get; set; } = string.Empty;       // solo en historial de grupo
    public bool HasWho => !string.IsNullOrEmpty(Who);
    public string Verdict { get; set; } = string.Empty;   // "✓" / "✗" / ""
    public bool HasVerdict => !string.IsNullOrEmpty(Verdict);
    public Color VerdictColor => Verdict == "✓" ? Colors.MediumSeaGreen : Colors.Orange;
    public bool HasPhoto { get; set; }
    [ObservableProperty] private ImageSource? photo;
}

/// Historial laboral: tareas pasadas con su foto de comprobante + sello ✓/✗.
public partial class HistoryViewModel : ObservableObject
{
    private readonly GameDataService _game;
    public ObservableCollection<HistoryEntry> Items { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isEmpty;
    public Guid? GroupId { get; set; }

    public HistoryViewModel(GameDataService game) => _game = game;

    public async Task LoadAsync()
    {
        IsBusy = true;
        Items.Clear();
        var list = await _game.GetHistoryAsync(GroupId);
        foreach (var h in list)
        {
            var e = new HistoryEntry
            {
                Description = h.Description,
                DateText = h.CreatedAt.ToLocalTime().ToString("dd MMM · HH:mm"),
                RewardText = L.F("+{0} XP · +{1} Oro", h.XpEarned, h.GoldEarned),
                Who = h.Username ?? string.Empty,
                Verdict = h.ProofVerdict == "ok" ? "✓" : h.ProofVerdict == "fail" ? "✗" : "",
                HasPhoto = h.ProofId.HasValue
            };
            Items.Add(e);
            if (h.ProofId.HasValue) _ = LoadPhotoAsync(e, h.ProofId.Value);
        }
        IsEmpty = Items.Count == 0;
        IsBusy = false;
    }

    private async Task LoadPhotoAsync(HistoryEntry e, Guid proofId)
    {
        var bytes = await _game.GetProofImageAsync(proofId);
        if (bytes != null) e.Photo = ImageSource.FromStream(() => new MemoryStream(bytes));
    }
}
