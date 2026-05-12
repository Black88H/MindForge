using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public record DailyStudyStat(DateTime Date, int MinutesStudied, int XPEarned, int SessionCount);
public record SubjectActivity(string SubjectName, int Minutes, int XP);
public record AnalyticsSummary(
    int TotalMinutes,
    int TotalSessions,
    int TotalXP,
    int CurrentStreak,
    int TotalTests,
    double AverageScore,
    int TotalTokens,
    IReadOnlyList<DailyStudyStat> Last7Days,
    IReadOnlyList<SubjectActivity> BySubject);

public interface IAnalyticsService
{
    Task<AnalyticsSummary> GetSummaryAsync(Guid userId);
    Task RecordStudySessionAsync(Guid userId, Guid? notebookId, int minutes, string sessionType);
    Task IncrementDailyStatAsync(Guid userId, Action<StudyStatistic> update);
}
