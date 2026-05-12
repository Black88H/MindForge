using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public record SM17ReviewResult(
    DateTime NextReviewDate,
    double   Stability,
    double   Retrievability,
    double   Difficulty,
    int      OptimalIntervalDays);

public record RetentionSummary(
    int    TotalItems,
    int    DueNow,
    double AverageRetention,
    int    PredictedDueTomorrow,
    int    PredictedDueWeek);

public interface ISpacedRepetitionService
{
    // Original SM-2 API (kept for compatibility)
    Task<List<SpacedRepetitionItem>> GetDueItemsAsync(Guid userId);
    Task ProcessReviewAsync(Guid itemId, int quality);
    Task<SpacedRepetitionItem> AddItemAsync(Guid userId, Guid knowledgeNodeId);

    // SM-17 extensions
    Task<SM17ReviewResult> ProcessSM17ReviewAsync(Guid itemId, int grade, double responseSeconds);
    Task<List<SpacedRepetitionItem>> GetOptimalQueueAsync(Guid userId, int max = 30);
    Task<RetentionSummary> GetRetentionSummaryAsync(Guid userId);
}
