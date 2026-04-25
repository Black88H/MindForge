using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class AnalyticsService
{
    private readonly MindForgeDbContext _db;
    private readonly GamificationService _gamification;

    public AnalyticsService(MindForgeDbContext db, GamificationService gamification)
    {
        _db = db;
        _gamification = gamification;
    }

    public async Task<List<(DateTime Date, int XP)>> GetXPProgressAsync(string userId, int days = 30)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && n.Type == NotificationType.XPEarned && n.Timestamp >= since)
            .OrderBy(n => n.Timestamp)
            .ToListAsync();

        return notifications
            .GroupBy(n => n.Timestamp.Date)
            .Select(g => (g.Key, g.Sum(n => n.XpAmount ?? 0)))
            .ToList();
    }

    public async Task<List<(string SubjectName, double SuccessRate, int TimeMinutes, double Progress)>>
        GetSubjectBreakdownAsync(string userId)
    {
        var progresses = await _db.UserProgress
            .Include(p => p.Subject)
            .Where(p => p.UserId == userId && p.Subject != null)
            .ToListAsync();

        return progresses
            .Where(p => p.Subject != null)
            .Select(p => (
                p.Subject!.Name,
                p.SuccessRate,
                p.TimeSpentMinutes,
                p.Subject!.Progress
            ))
            .OrderByDescending(x => x.SuccessRate)
            .ToList();
    }

    public async Task<List<(string SubjectName, double Hours)>> GetTimeTrackingAsync(string userId)
    {
        var progresses = await _db.UserProgress
            .Include(p => p.Subject)
            .Where(p => p.UserId == userId && p.Subject != null)
            .ToListAsync();

        return progresses
            .Where(p => p.Subject != null)
            .Select(p => (p.Subject!.Name, p.TimeSpentMinutes / 60.0))
            .OrderByDescending(x => x.Item2)
            .ToList();
    }

    public async Task<(int Completed, int Total)> GetAchievementStatsAsync()
    {
        var all = await _db.Achievements.ToListAsync();
        return (all.Count(a => a.IsUnlocked), all.Count);
    }

    public async Task<List<string>> GenerateRecommendationsAsync(string userId)
    {
        var progress = await _db.UserProgress
            .Include(p => p.Subject)
            .Where(p => p.UserId == userId)
            .ToListAsync();

        var recommendations = new List<string>();

        var weakest = progress
            .Where(p => p.Subject != null && p.QuestionsAnswered > 0)
            .OrderBy(p => p.SuccessRate)
            .FirstOrDefault();

        if (weakest?.Subject != null)
            recommendations.Add($"Übe mehr bei {weakest.Subject.Name} — deine Erfolgsrate beträgt {weakest.SuccessRate * 100:F0}%");

        var noStreak = progress.Any(p => p.CurrentStreak == 0);
        if (noStreak)
            recommendations.Add("Starte heute eine neue Streak! Tägliches Lernen verbessert die Retention um 40%.");

        var dueCount = await _db.SpacedRepetitionItems
            .CountAsync(sri => sri.NextReviewDate.Date <= DateTime.UtcNow.Date);
        if (dueCount > 0)
            recommendations.Add($"Du hast {dueCount} Wiederholungskarten fällig — jetzt wiederholen!");

        if (!recommendations.Any())
            recommendations.Add("Großartig! Erstelle einen Lernplan für dein nächstes Fach.");

        return recommendations;
    }

    public async Task<List<(string Subject, int Correct, int Total, double SuccessRate)>>
        GetTestHistoryStatsAsync(string userId)
    {
        var history = await _db.UserTestHistory
            .Include(uth => uth.Test)
            .ThenInclude(t => t!.Subject)
            .Where(uth => uth.UserId == userId)
            .ToListAsync();

        return history
            .Where(uth => uth.Test?.Subject != null)
            .GroupBy(uth => uth.Test!.Subject!.Name)
            .Select(g => (
                g.Key,
                g.Sum(x => x.CorrectAnswers),
                g.Sum(x => x.TotalQuestions),
                g.Average(x => x.Score)
            ))
            .ToList();
    }
}
