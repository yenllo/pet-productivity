using CommunityToolkit.Mvvm.ComponentModel;
using PetProductivity.Shared.Models;
using System.Collections.ObjectModel;

namespace PetProductivity.Client.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        private readonly Services.GameDataService _gameDataService;

        // Antes esta pantalla mostraba un "Nivel" (TotalXp/1000+1) inventado y desconectado de la
        // etapa real de la mascota (Huevo/Cría/Adulto/Maestro) que se ve en Mascota — ver PetVisuals.
        [ObservableProperty]
        private string stageLabel = string.Empty;

        [ObservableProperty]
        private int totalXp;

        [ObservableProperty]
        private int tasksCompleted;

        [ObservableProperty]
        private int currentStreak;

        [ObservableProperty]
        private double stageProgress;

        [ObservableProperty]
        private ObservableCollection<StatDisplay> petStats = new();

        public StatsViewModel(Services.GameDataService gameDataService)
        {
            _gameDataService = gameDataService;
            _ = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            await _gameDataService.InitializeAsync();
            var user = _gameDataService.CurrentUser;
            if (user != null)
            {
                 CurrentStreak = user.CurrentStreak;
                 TasksCompleted = user.TotalTasksCompleted;

                 // Pet Stats
                 if (user.UserPet != null)
                 {
                     TotalXp = (int)user.UserPet.TotalXp;
                     StageLabel = Services.PetVisuals.StageName(user.UserPet.EvolutionStage);
                     StageProgress = Services.PetVisuals.StageProgress(user.UserPet.EvolutionStage, user.UserPet.TotalXp);

                     PetStats.Clear();
                     foreach (var kvp in user.UserPet.Stats.OrderByDescending(x => x.Value))
                     {
                         PetStats.Add(new StatDisplay { Name = Services.PetVisuals.StatDisplayName(kvp.Key), Value = kvp.Value, Icon = "✨" });
                     }
                 }
            }
        }
    }
    
    public class StatDisplay
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Icon { get; set; } = string.Empty;
    }
}
