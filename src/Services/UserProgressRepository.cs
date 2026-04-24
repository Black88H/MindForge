using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class UserProgressRepository
{
    private readonly MindForgeDbContext _db;
    private const string DefaultUser = "default";

    public UserProgressRepository(MindForgeDbContext db) => _db = db;

    public async Task<UserProgress> GetGlobalProgressAsync()
    {
        var p = await _db.UserProgress
            .FirstOrDefaultAsync(x => x.UserId == DefaultUser && x.SubjectId == null);

        if (p == null)
        {
            p = new UserProgress { UserId = DefaultUser };
            _db.UserProgress.Add(p);
            await _db.SaveChangesAsync();
        }
        return p;
    }

    public async Task<UserProgress?> GetSubjectProgressAsync(Guid subjectId)
        => await _db.UserProgress
            .FirstOrDefaultAsync(x => x.UserId == DefaultUser && x.SubjectId == subjectId);

    public async Task RecordCorrectAnswerAsync(Guid subjectId)
    {
        var global = await GetGlobalProgressAsync();
        global.QuestionsAnswered++;
        global.CorrectAnswers++;
        global.TotalToday++;
        global.CorrectToday++;
        global.TotalXP += Utils.Constants.XP.CorrectAnswer;
        global.Level = CalculateLevel(global.TotalXP);
        global.LastStudied = DateTime.UtcNow;

        var sub = await GetOrCreateSubjectProgressAsync(subjectId);
        sub.QuestionsAnswered++;
        sub.CorrectAnswers++;
        sub.TotalToday++;
        sub.CorrectToday++;
        sub.LastStudied = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task RecordWrongAnswerAsync(Guid subjectId)
    {
        var global = await GetGlobalProgressAsync();
        global.QuestionsAnswered++;
        global.TotalToday++;
        global.LastStudied = DateTime.UtcNow;

        var sub = await GetOrCreateSubjectProgressAsync(subjectId);
        sub.QuestionsAnswered++;
        sub.TotalToday++;

        await _db.SaveChangesAsync();
    }

    public async Task<bool> CheckAndUpdateStreakAsync()
    {
        var global = await GetGlobalProgressAsync();
        var today = DateTime.UtcNow.Date;
        var last = global.LastStreakDate.Date;

        if (last == today) return false;

        if (last == today.AddDays(-1))
        {
            global.CurrentStreak++;
            if (global.CurrentStreak > global.BestStreak)
                global.BestStreak = global.CurrentStreak;
        }
        else if (last < today.AddDays(-1))
        {
            global.CurrentStreak = 1;
        }

        global.LastStreakDate = today;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task AddXPAsync(int amount)
    {
        var global = await GetGlobalProgressAsync();
        global.TotalXP += amount;
        global.Level = CalculateLevel(global.TotalXP);
        await _db.SaveChangesAsync();
    }

    public async Task<List<(DateTime Date, int XP)>> GetXPHistoryAsync(int days)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        var answers = await _db.Answers
            .Where(a => a.Timestamp >= since && a.IsCorrect)
            .GroupBy(a => a.Timestamp.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return answers.Select(x => (x.Date, x.Count * Utils.Constants.XP.CorrectAnswer)).ToList();
    }

    private async Task<UserProgress> GetOrCreateSubjectProgressAsync(Guid subjectId)
    {
        var p = await GetSubjectProgressAsync(subjectId);
        if (p == null)
        {
            p = new UserProgress { UserId = DefaultUser, SubjectId = subjectId };
            _db.UserProgress.Add(p);
        }
        return p;
    }

    private static int CalculateLevel(int totalXP)
        => Math.Max(1, (int)Math.Sqrt(totalXP / 100.0) + 1);
}
