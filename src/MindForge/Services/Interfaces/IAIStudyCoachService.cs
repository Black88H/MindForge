using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public record CoachRecommendation(
    string Title,
    string Description,
    string Priority,      // "high" | "medium" | "low"
    string Category,      // "time-management" | "study-technique" | "content-focus" | "motivation"
    List<string> ActionSteps);

public record CoachReport(
    List<CoachRecommendation> Recommendations,
    string WeeklyGoal,
    string Encouragement,
    string BurnoutRisk,   // "low" | "medium" | "high"
    int RecommendedDailyMinutes);

public interface IAIStudyCoachService
{
    Task<CoachReport> GetRecommendationsAsync(Guid userId);
    Task<string> GetQuickTipAsync(Guid userId);
}
