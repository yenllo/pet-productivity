using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace PetProductivity.Client.ViewModels
{
    public partial class StatsViewModel : ObservableObject
    {
        private readonly Services.GameDataService _gameDataService;
        
        [ObservableProperty]
        private int currentLevel;

        [ObservableProperty]
        private int currentXp;

        [ObservableProperty]
        private int xpToNextLevel = 1000;

        [ObservableProperty]
        private int tasksCompleted;

        [ObservableProperty]
        private int currentStreak;

        [ObservableProperty]
        private double levelProgress;
        
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
                     CurrentXp = (int)user.UserPet.TotalXp;
                     CurrentLevel = (int)(user.UserPet.TotalXp / 1000) + 1;
                     CurrentXp = (int)(user.UserPet.TotalXp % 1000);
                     LevelProgress = CurrentXp / 1000.0;
                     
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
