using MindForge.Models;

namespace MindForge.Services;

public interface IGamificationService
{
    int CurrentXP { get; }
    int CurrentLevel { get; }
    int XPToNextLevel { get; }
    int CurrentStreak { get; }
    int BestStreak { get; }

    Task<int> AddXPAsync(int amount, string reason);
    Task<bool> CheckStreakAsync();
    Task<IEnumerable<Achievement>> GetAchievementsAsync();
    Task<IEnumerable<Achievement>> CheckNewAchievementsAsync();
    event EventHandler<int> XPAdded;
    event EventHandler<int> LevelUp;
    event EventHandler<Achievement> AchievementUnlocked;
}
