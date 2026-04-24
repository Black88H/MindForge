using MindForge.Models;

namespace MindForge.Services;

public interface IAnalyticsService
{
    Task<IEnumerable<(DateTime Date, int XP)>> GetXPHistoryAsync(int days);
    Task<IEnumerable<(DateTime Date, int Questions)>> GetDailyActivityAsync(int days);
    Task<IEnumerable<(Subject Subject, double SuccessRate)>> GetSubjectStatsAsync();
    Task<IEnumerable<(string Subject, int Minutes)>> GetTimePerSubjectAsync();
    Task<Dictionary<string, int>> GetWeaknessAreasAsync();
}
