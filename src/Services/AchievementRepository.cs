using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class AchievementRepository : IGamificationService
{
    private readonly MindForgeDbContext _db;
    private readonly UserProgressRepository _progress;

    public AchievementRepository(MindForgeDbContext db, UserProgressRepository progress)
    {
        _db = db;
        _progress = progress;
    }

    public int CurrentXP => GetGlobalXP().GetAwaiter().GetResult();
    public int CurrentLevel => CalculateLevel(CurrentXP);
    public int XPToNextLevel => (CurrentLevel * CurrentLevel) * 100;
    public int CurrentStreak { get; private set; }
    public int BestStreak { get; private set; }

    public event EventHandler<int>? XPAdded;
    public event EventHandler<int>? LevelUp;
    public event EventHandler<Achievement>? AchievementUnlocked;

    public async Task<int> AddXPAsync(int amount, string reason)
    {
        var before = await GetGlobalXP();
        await _progress.AddXPAsync(amount);
        var after = await GetGlobalXP();

        XPAdded?.Invoke(this, amount);

        if (CalculateLevel(after) > CalculateLevel(before))
            LevelUp?.Invoke(this, CalculateLevel(after));

        return after;
    }

    public async Task<bool> CheckStreakAsync()
    {
        var updated = await _progress.CheckAndUpdateStreakAsync();
        var global = await _progress.GetGlobalProgressAsync();
        CurrentStreak = global.CurrentStreak;
        BestStreak = global.BestStreak;
        return updated;
    }

    public async Task<IEnumerable<Achievement>> GetAchievementsAsync()
        => await _db.Achievements.OrderBy(a => a.Rarity).ToListAsync();

    public async Task<IEnumerable<Achievement>> CheckNewAchievementsAsync()
    {
        var progress = await _progress.GetGlobalProgressAsync();
        var achievements = await _db.Achievements.Where(a => !a.IsUnlocked).ToListAsync();
        var unlocked = new List<Achievement>();

        foreach (var ach in achievements)
        {
            bool triggered = ach.TriggerKey switch
            {
                "questions_answered" => progress.QuestionsAnswered >= ach.TriggerValue,
                "streak_days"        => progress.CurrentStreak >= ach.TriggerValue,
                _                    => false,
            };

            if (triggered)
            {
                ach.IsUnlocked = true;
                ach.UnlockedAt = DateTime.UtcNow;
                unlocked.Add(ach);
                await AddXPAsync(ach.XpReward, $"Achievement: {ach.Name}");
                AchievementUnlocked?.Invoke(this, ach);
            }
        }

        if (unlocked.Count > 0)
            await _db.SaveChangesAsync();

        return unlocked;
    }

    private async Task<int> GetGlobalXP()
    {
        var p = await _progress.GetGlobalProgressAsync();
        return p.TotalXP;
    }

    private static int CalculateLevel(int totalXP) => Math.Max(1, (int)Math.Sqrt(totalXP / 100.0) + 1);
}
