using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class PredictiveAnalyticsService : IPredictiveAnalyticsService
{
    private readonly MindForgeDbContext _db;

    public PredictiveAnalyticsService(MindForgeDbContext db) => _db = db;

    public async Task<LearningPredictions> PredictAsync(Guid userId, int daysAhead = 14)
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-30);
        var stats = await _db.StudyStatistics
            .Where(s => s.UserId == userId && s.Date >= cutoff)
            .OrderBy(s => s.Date)
            .ToListAsync();

        if (stats.Count < 3)
        {
            return new LearningPredictions(
                BuildFlatPredictions(daysAhead, 0, 30),
                "stable", "stable", "low", 30,
                "Noch zu wenig Daten fuer eine genaue Vorhersage. Lerne mindestens 3 Tage.",
                "Sammle mehr Lernhistorie fuer bessere Prognosen.");
        }

        // Linear regression on score
        var scoreSamples = stats
            .Where(s => s.TestsTaken > 0)
            .Select((s, i) => ((double)i, s.AverageScore))
            .ToList();

        var (scoreSlope, scoreIntercept) = LinearRegression(scoreSamples);

        // Linear regression on minutes
        var minuteSamples = stats
            .Select((s, i) => ((double)i, (double)s.MinutesStudied))
            .ToList();

        var (minuteSlope, minuteIntercept) = LinearRegression(minuteSamples);

        int n = stats.Count;
        var predictions = new List<DailyPrediction>();
        for (int d = 1; d <= daysAhead; d++)
        {
            double x           = n + d - 1;
            double rawScore    = scoreSlope * x + scoreIntercept;
            double rawMinutes  = minuteSlope * x + minuteIntercept;
            double confidence  = Math.Max(0.3, 1.0 - d * 0.04);

            predictions.Add(new DailyPrediction(
                DateTime.UtcNow.Date.AddDays(d),
                Math.Clamp(rawScore,   0, 100),
                Math.Max(0, (int)rawMinutes),
                confidence));
        }

        // Trends
        string perfTrend   = scoreSlope  >  0.3 ? "improving" : scoreSlope  < -0.3 ? "declining" : "stable";
        string minuteTrend = minuteSlope >  1.0 ? "improving" : minuteSlope < -1.0 ? "declining" : "stable";

        // Burnout risk: recent 7-day avg vs previous 7-day avg
        var recent7  = stats.TakeLast(7).Average(s => s.MinutesStudied);
        var prev7    = stats.Count >= 14
            ? stats.Skip(stats.Count - 14).Take(7).Average(s => s.MinutesStudied)
            : recent7;

        string burnout = prev7 > 0 && recent7 / prev7 > 1.4 ? "high"
            : prev7 > 0 && recent7 / prev7 > 1.2 ? "medium"
            : "low";

        int recMinutes = Math.Clamp((int)(recent7 * 1.1), 20, 120);

        double projectedScore = predictions.Last().PredictedScore;
        string masteryMsg = projectedScore >= 85
            ? "Du wirst in 2 Wochen Meisterschaftslevel erreichen! Weiter so."
            : projectedScore >= 70
                ? $"Prognose: {projectedScore:F0}% Durchschnitt in {daysAhead} Tagen. Gute Fortschritte!"
                : $"Prognose: {projectedScore:F0}% Durchschnitt. Intensiviere dein Training fuer bessere Ergebnisse.";

        return new LearningPredictions(predictions, perfTrend, minuteTrend, burnout, recMinutes, masteryMsg);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (double slope, double intercept) LinearRegression(List<(double x, double y)> points)
    {
        if (points.Count < 2) return (0, points.Any() ? points[0].y : 0);

        double n    = points.Count;
        double sumX = points.Sum(p => p.x);
        double sumY = points.Sum(p => p.y);
        double sumXY = points.Sum(p => p.x * p.y);
        double sumX2 = points.Sum(p => p.x * p.x);

        double denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-10) return (0, sumY / n);

        double slope     = (n * sumXY - sumX * sumY) / denom;
        double intercept = (sumY - slope * sumX) / n;
        return (slope, intercept);
    }

    private static List<DailyPrediction> BuildFlatPredictions(int days, double score, int minutes)
        => Enumerable.Range(1, days)
            .Select(d => new DailyPrediction(DateTime.UtcNow.Date.AddDays(d), score, minutes, 0.5))
            .ToList();
}
