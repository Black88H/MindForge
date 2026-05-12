using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly MindForgeDbContext _db;

    public AnalyticsService(MindForgeDbContext db) => _db = db;

    public async Task<AnalyticsSummary> GetSummaryAsync(Guid userId)
    {
        var today  = DateTime.UtcNow.Date;
        var cutoff = today.AddDays(-6); // last 7 days

        // Daily stats
        var stats = await _db.StudyStatistics
            .Where(s => s.UserId == userId && s.Date >= cutoff)
            .OrderBy(s => s.Date)
            .ToListAsync();

        // Fill missing days
        var last7 = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .Select(d =>
            {
                var s = stats.FirstOrDefault(x => x.Date.Date == d);
                return new DailyStudyStat(d,
                    s?.MinutesStudied ?? 0,
                    s?.XPEarned ?? 0,
                    s?.SessionCount ?? 0);
            })
            .ToList();

        // Totals
        var allStats = await _db.StudyStatistics
            .Where(s => s.UserId == userId)
            .ToListAsync();

        int totalMinutes  = allStats.Sum(s => s.MinutesStudied);
        int totalSessions = allStats.Sum(s => s.SessionCount);
        int totalXP       = allStats.Sum(s => s.XPEarned);
        int totalTests    = allStats.Sum(s => s.TestsTaken);
        double avgScore   = allStats.Any(s => s.TestsTaken > 0)
            ? allStats.Where(s => s.TestsTaken > 0).Average(s => s.AverageScore)
            : 0;

        // Token usage total
        var totalTokens = await _db.TokenUsages
            .Where(t => t.UserId == userId)
            .SumAsync(t => t.PromptTokens + t.CompletionTokens);

        // Streak: count consecutive days with MinutesStudied > 0 working back from today
        var orderedDays = allStats
            .Where(s => s.MinutesStudied > 0)
            .Select(s => s.Date.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        int streak = 0;
        var check  = today;
        foreach (var day in orderedDays)
        {
            if (day == check || day == check.AddDays(-1))
            {
                streak++;
                check = day;
            }
            else break;
        }

        // Per-subject breakdown: XP earned per subject based on XPEvents description
        var xpEvents = await _db.XPEvents
            .Where(x => x.UserId == userId)
            .ToListAsync();

        var subjects = await _db.Subjects
            .Where(s => s.UserId == userId)
            .ToListAsync();

        var bySubject = subjects
            .Select(s =>
            {
                var subjectXP = xpEvents
                    .Where(x => x.Description.Contains(s.Name, StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount);
                return new SubjectActivity(s.Name, 0, subjectXP);
            })
            .Where(sa => sa.XP > 0)
            .OrderByDescending(sa => sa.XP)
            .Take(5)
            .ToList();

        return new AnalyticsSummary(
            totalMinutes, totalSessions, totalXP, streak,
            totalTests, avgScore, totalTokens,
            last7, bySubject);
    }

    public async Task RecordStudySessionAsync(
        Guid userId, Guid? notebookId, int minutes, string sessionType)
    {
        _db.StudySessions.Add(new StudySession
        {
            Id              = Guid.NewGuid(),
            UserId          = userId,
            NotebookId      = notebookId,
            SessionType     = sessionType,
            StartedAt       = DateTime.UtcNow.AddMinutes(-minutes),
            EndedAt         = DateTime.UtcNow,
            DurationMinutes = minutes,
            Completed       = true,
            XPEarned        = minutes * 2
        });
        await _db.SaveChangesAsync();

        await IncrementDailyStatAsync(userId, s =>
        {
            s.MinutesStudied += minutes;
            s.SessionCount++;
            s.XPEarned += minutes * 2;
        });
    }

    public async Task IncrementDailyStatAsync(Guid userId, Action<StudyStatistic> update)
    {
        var today = DateTime.UtcNow.Date;
        var stat  = await _db.StudyStatistics
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Date == today);
        if (stat is null)
        {
            stat = new StudyStatistic { Id = Guid.NewGuid(), UserId = userId, Date = today };
            _db.StudyStatistics.Add(stat);
        }
        update(stat);
        await _db.SaveChangesAsync();
    }
}
