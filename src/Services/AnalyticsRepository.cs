using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class AnalyticsRepository : IAnalyticsService
{
    private readonly MindForgeDbContext _db;

    public AnalyticsRepository(MindForgeDbContext db) => _db = db;

    public async Task<IEnumerable<(DateTime Date, int XP)>> GetXPHistoryAsync(int days)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var raw = await _db.Answers
            .Where(a => a.Timestamp >= since && a.IsCorrect)
            .GroupBy(a => a.Timestamp.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return raw.Select(x => (x.Date, x.Count * Utils.Constants.XP.CorrectAnswer));
    }

    public async Task<IEnumerable<(DateTime Date, int Questions)>> GetDailyActivityAsync(int days)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var raw = await _db.Answers
            .Where(a => a.Timestamp >= since)
            .GroupBy(a => a.Timestamp.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return raw.Select(x => (x.Date, x.Count));
    }

    public async Task<IEnumerable<(Subject Subject, double SuccessRate)>> GetSubjectStatsAsync()
    {
        var subjects = await _db.Subjects.ToListAsync();
        var result = new List<(Subject, double)>();

        foreach (var s in subjects)
        {
            var questions = await _db.Questions
                .Where(q => q.SubjectId == s.Id && q.TimesAnswered > 0)
                .ToListAsync();

            var rate = questions.Count > 0 ? questions.Average(q => q.SuccessRate) : 0;
            result.Add((s, rate));
        }

        return result.OrderByDescending(x => x.Item2);
    }

    public async Task<IEnumerable<(string Subject, int Minutes)>> GetTimePerSubjectAsync()
    {
        var raw = await _db.Answers
            .Include(a => a.Question)
            .ThenInclude(q => q!.Subject)
            .GroupBy(a => a.Question!.Subject!.Name)
            .Select(g => new { Name = g.Key, TotalSec = g.Sum(a => a.TimeSpentSeconds) })
            .ToListAsync();

        return raw.Select(x => (x.Name, x.TotalSec / 60));
    }

    public async Task<Dictionary<string, int>> GetWeaknessAreasAsync()
    {
        var weak = await _db.Questions
            .Where(q => q.TimesAnswered >= 3 && (double)q.TimesCorrect / q.TimesAnswered < 0.6)
            .Include(q => q.Subject)
            .GroupBy(q => q.Subject!.Name)
            .Select(g => new { Subject = g.Key, Count = g.Count() })
            .ToListAsync();

        return weak.ToDictionary(x => x.Subject, x => x.Count);
    }

    public async Task<int> GetTotalQuestionsAnsweredAsync()
        => await _db.Answers.CountAsync();

    public async Task<double> GetOverallSuccessRateAsync()
    {
        var total = await _db.Answers.CountAsync();
        if (total == 0) return 0;
        var correct = await _db.Answers.CountAsync(a => a.IsCorrect);
        return (double)correct / total;
    }
}
