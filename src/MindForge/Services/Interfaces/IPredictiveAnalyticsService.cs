using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public record DailyPrediction(DateTime Date, double PredictedScore, int PredictedMinutes, double Confidence);

public record LearningPredictions(
    List<DailyPrediction> Predictions,
    string PerformanceTrend,    // "improving" | "stable" | "declining"
    string StudyTimeTrend,
    string BurnoutRisk,
    int    RecommendedDailyMinutes,
    string EstimatedMasteryMessage,
    string Message = "");

public interface IPredictiveAnalyticsService
{
    Task<LearningPredictions> PredictAsync(Guid userId, int daysAhead = 14);
}
