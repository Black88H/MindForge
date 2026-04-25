using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Utils;

namespace MindForge.Services;

public class GamificationService
{
    private readonly MindForgeDbContext _db;

    public GamificationService(MindForgeDbContext db) => _db = db;

    public async Task<int> AddXPAsync(string userId, XPAction action, int? customAmount = null)
    {
        var progress = await GetOrCreateProgressAsync(userId);
        int xp = customAmount ?? GetXPForAction(action, progress.Level);
        progress.TotalXP += xp;
        progress.Level = CalculateLevel(progress.TotalXP);

        var notification = new Notification
        {
            UserId = userId,
            Message = $"+{xp} XP für {GetActionLabel(action)}",
            Type = NotificationType.XPEarned,
            XpAmount = xp
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        await CheckAchievementsAsync(userId, progress);
        return xp;
    }

    public int CalculateLevel(int totalXp) => Math.Max(1, Math.Min(100, (int)Math.Sqrt(totalXp / 50.0) + 1));

    public int GetXPForNextLevel(int level) => (int)Math.Pow(level, 2) * 50;

    public async Task<int> UpdateStreakAsync(string userId)
    {
        var progress = await GetOrCreateProgressAsync(userId);
        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        if (progress.LastStreakDate.Date == today)
            return progress.CurrentStreak;

        if (progress.LastStreakDate.Date == yesterday)
        {
            progress.CurrentStreak++;
        }
        else
        {
            progress.CurrentStreak = 1;
        }

        if (progress.CurrentStreak > progress.BestStreak)
            progress.BestStreak = progress.CurrentStreak;

        progress.LastStreakDate = today;
        await _db.SaveChangesAsync();

        if (progress.CurrentStreak is 7 or 30 or 100)
            await AddXPAsync(userId, XPAction.StreakMilestone, progress.CurrentStreak * 10);

        return progress.CurrentStreak;
    }

    public async Task<List<Achievement>> CheckAchievementsAsync(string userId, UserProgress? progress = null)
    {
        progress ??= await GetOrCreateProgressAsync(userId);
        var unlocked = new List<Achievement>();
        var allAchievements = await _db.Achievements.ToListAsync();

        foreach (var ach in allAchievements.Where(a => !a.IsUnlocked))
        {
            bool shouldUnlock = ach.TriggerKey switch
            {
                "questions_answered" => progress.QuestionsAnswered >= ach.TriggerValue,
                "streak_days"        => progress.CurrentStreak >= ach.TriggerValue,
                "study_hour"         => DateTime.Now.Hour >= ach.TriggerValue,
                _                    => false
            };

            if (shouldUnlock)
            {
                ach.IsUnlocked = true;
                ach.UnlockedAt = DateTime.UtcNow;
                unlocked.Add(ach);

                _db.Notifications.Add(new Notification
                {
                    UserId = userId,
                    Message = $"Achievement freigeschaltet: {ach.Name} {ach.Icon}",
                    Type = NotificationType.Success,
                    XpAmount = ach.XpReward
                });
                progress.TotalXP += ach.XpReward;
            }
        }

        if (unlocked.Count > 0)
            await _db.SaveChangesAsync();

        return unlocked;
    }

    public async Task<Challenge> CreateDailyChallengeAsync()
    {
        var templates = new[]
        {
            ("10 Fragen beantworten", "Beantworte heute 10 Fragen richtig", "🎯", 100, 10),
            ("Streak halten", "Lerne heute und halte deinen Streak", "🔥", 50, 1),
            ("Schnellläufer", "Beantworte 5 Fragen in unter 30 Sekunden", "⚡", 150, 5),
            ("Perfekter Block", "Beantworte 5 Fragen in Folge richtig", "💯", 200, 5),
            ("Material hochladen", "Lade ein Lernmaterial hoch", "📄", 75, 1),
        };
        var rnd = templates[Random.Shared.Next(templates.Length)];
        var challenge = new Challenge
        {
            Title = rnd.Item1,
            Description = rnd.Item2,
            Icon = rnd.Item3,
            XpReward = rnd.Item4,
            Type = ChallengeType.Daily,
            RequiredProgress = rnd.Item5,
            ExpiresDate = DateTime.UtcNow.Date.AddDays(1)
        };
        _db.Challenges.Add(challenge);
        await _db.SaveChangesAsync();
        return challenge;
    }

    public async Task<bool> CompleteChallengeAsync(string userId, Guid challengeId)
    {
        var uc = await _db.UserChallenges
            .Include(u => u.Challenge)
            .FirstOrDefaultAsync(u => u.UserId == userId && u.ChallengeId == challengeId);

        if (uc == null || uc.Completed) return false;

        uc.Completed = true;
        uc.CompletedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        if (uc.Challenge != null)
            await AddXPAsync(userId, XPAction.ChallengeComplete, uc.Challenge.XpReward);

        return true;
    }

    private async Task<UserProgress> GetOrCreateProgressAsync(string userId)
    {
        var progress = await _db.UserProgress.FirstOrDefaultAsync(p => p.UserId == userId);
        if (progress == null)
        {
            progress = new UserProgress { UserId = userId };
            _db.UserProgress.Add(progress);
            await _db.SaveChangesAsync();
        }
        return progress;
    }

    private static int GetXPForAction(XPAction action, int level) => action switch
    {
        XPAction.CorrectAnswer    => Constants.XP.CorrectAnswer,
        XPAction.QuizComplete     => 5 + level / 2,
        XPAction.TestComplete     => Constants.XP.TestCompleted,
        XPAction.TestPerfect      => Constants.XP.TestPerfect,
        XPAction.ChallengeComplete => Constants.XP.ChallengeMin,
        XPAction.ContentGenerated  => Constants.XP.ContentGenerated,
        XPAction.StreakMilestone   => Constants.XP.StreakBonus * 5,
        XPAction.PlanCreated       => Constants.XP.LearningPlanCreated,
        _ => 10
    };

    private static string GetActionLabel(XPAction action) => action switch
    {
        XPAction.CorrectAnswer     => "richtige Antwort",
        XPAction.QuizComplete      => "Quiz abgeschlossen",
        XPAction.TestComplete      => "Test abgeschlossen",
        XPAction.TestPerfect       => "perfekten Test",
        XPAction.ChallengeComplete => "Challenge abgeschlossen",
        XPAction.ContentGenerated  => "Content erstellt",
        XPAction.StreakMilestone   => "Streak-Meilenstein",
        XPAction.PlanCreated       => "Lernplan erstellt",
        _ => "Aktion"
    };
}

public enum XPAction
{
    CorrectAnswer,
    QuizComplete,
    TestComplete,
    TestPerfect,
    ChallengeComplete,
    ContentGenerated,
    StreakMilestone,
    PlanCreated
}
