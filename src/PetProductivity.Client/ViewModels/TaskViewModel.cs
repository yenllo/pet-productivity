using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PetProductivity.Shared.Models;
using PetProductivity.Client.Services;
using System.Collections.ObjectModel;

namespace PetProductivity.Client.ViewModels;

[QueryProperty(nameof(PetId), "petId")]
[QueryProperty(nameof(PetName), "petName")]
[QueryProperty(nameof(PetImage), "petImage")]
public partial class TaskViewModel : ObservableObject
{
    // Mascota destino: vacío = personal; con valor = mascota de grupo.
    [ObservableProperty] private string petId = string.Empty;
    [ObservableProperty] private string petName = string.Empty;
    [ObservableProperty] private string petImage = string.Empty;

    [ObservableProperty] private string taskInput = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;
    public bool IsNotBusy => !IsBusy;

    [ObservableProperty] private string aiFeedbackMessage = string.Empty;

    // T27-L3 (#20): celebración de recompensa (overlay). La página anima la tarjeta al aparecer.
    [ObservableProperty] private bool showCelebration;
    [ObservableProperty] private string celebStars = string.Empty;
    [ObservableProperty] private string celebXp = string.Empty;
    [ObservableProperty] private string celebGold = string.Empty;
    [ObservableProperty] private string celebNote = string.Empty;

    [RelayCommand]
    private void DismissCelebration() => ShowCelebration = false;
    // Registro = feed del servidor: tareas (normales y de foco) con su foto y el ✓/✗ de Gemini.
    [ObservableProperty] private ObservableCollection<HistoryEntry> recentTasks = new();

    private readonly GameDataService _gameDataService;
    private readonly FocusSessionService _focus;

    public TaskViewModel(GameDataService gameDataService, FocusSessionService focus)
    {
        _gameDataService = gameDataService;
        _focus = focus;
        _ = RefreshFeedAsync();
    }

    public async Task RefreshFeedAsync()
    {
        var list = await _gameDataService.GetHistoryAsync();
        RecentTasks.Clear();
        foreach (var h in list)
        {
            var e = new HistoryEntry
            {
                Description = h.Description,
                DateText = h.CreatedAt.ToLocalTime().ToString("dd MMM · HH:mm"),
                RewardText = L.F("+{0} XP · +{1} Oro", h.XpEarned, h.GoldEarned),
                Verdict = h.ProofVerdict == "ok" ? "✓" : h.ProofVerdict == "fail" ? "✗" : "",
                HasPhoto = h.ProofId.HasValue
            };
            RecentTasks.Add(e);
            if (h.ProofId.HasValue) _ = LoadPhotoAsync(e, h.ProofId.Value);
        }
    }

    private async Task LoadPhotoAsync(HistoryEntry e, Guid proofId)
    {
        var bytes = await _gameDataService.GetProofImageAsync(proofId);
        if (bytes != null) e.Photo = ImageSource.FromStream(() => new MemoryStream(bytes));
    }

    [RelayCommand]
    public async Task SubmitTask()
    {
        if (string.IsNullOrWhiteSpace(TaskInput)) return;

        IsBusy = true;
        AiFeedbackMessage = L.T("La IA está juzgando tu esfuerzo...");

        try
        {
            var targetPet = Guid.TryParse(PetId, out var g) ? g : Guid.Empty;
            var result = await _gameDataService.CompleteTaskAsync(TaskInput, targetPet, false);

            if (result.NeedsConfirmation)
            {
                bool yes = await Shell.Current.DisplayAlert(L.T("Fuera de contexto"), result.Message, L.T("Registrar igual"), L.T("Cancelar"));
                if (!yes) { IsBusy = false; return; }
                result = await _gameDataService.CompleteTaskAsync(TaskInput, targetPet, true);
            }

            if (result.Queued)
            {
                // T13: sin red — guardada en la cola offline; no refrescar el feed (también fallaría).
                AiFeedbackMessage = result.Message;
                TaskInput = string.Empty;
                return;
            }

            if (result.IsRevived)
                AiFeedbackMessage = L.T("🔥 ¡RENACIMIENTO! 🔥") + "\n" + result.Message;
            else if (result.Message.Contains("sacrificio mayor"))
                AiFeedbackMessage = L.T("⚠️ CRISTALIZADO ⚠️") + "\n" + result.Message;
            else
            {
                // #20: overlay de celebración en vez del label plano.
                AiFeedbackMessage = string.Empty;
                var d = Math.Clamp(result.DifficultyScore, 0, 10);
                CelebStars = new string('★', d) + new string('☆', 10 - d);
                CelebXp = L.F("+{0} XP", result.XpEarned) + (result.WasReducedReward ? L.T(" (reducido)") : "");
                CelebGold = L.F("+{0} Oro", result.GoldEarned);
                CelebNote = result.Message; // feedback emocional de la IA (GameDataService ya lo puso aquí)
                ShowCelebration = true;
                try { HapticFeedback.Default.Perform(HapticFeedbackType.LongPress); } catch { }
            }

            TaskInput = string.Empty;
            await RefreshFeedAsync();
        }
        catch (Exception ex)
        {
            AiFeedbackMessage = L.F("Error técnico: {0}", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Modo foco es su propia página (apartado): se le pasa la tarea como "tema" + la mascota.
    [RelayCommand]
    private async Task GoToFocus()
    {
        // Ya hay una sesión corriendo (p. ej. se salió al menú sin cerrarla): entrar directo, sin pedir
        // describir la tarea otra vez — antes esto parecía "olvidar" que el foco seguía activo, porque
        // exigía una descripción nueva antes de dejar volver a la sesión que ya existía.
        if (_focus.IsActive)
        {
            await Shell.Current.GoToAsync("FocusPage");
            return;
        }

        if (string.IsNullOrWhiteSpace(TaskInput))
        {
            AiFeedbackMessage = L.T("Escribe qué vas a hacer antes de iniciar el foco.");
            return;
        }
        var url = $"FocusPage?petId={PetId}" +
                  $"&petName={Uri.EscapeDataString(PetName)}" +
                  $"&petImage={Uri.EscapeDataString(PetImage)}" +
                  $"&description={Uri.EscapeDataString(TaskInput)}";
        await Shell.Current.GoToAsync(url);
    }
}
