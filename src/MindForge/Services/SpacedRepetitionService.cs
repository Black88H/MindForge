using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class SpacedRepetitionService : ISpacedRepetitionService
{
    private readonly MindForgeDbContext _db;

    public SpacedRepetitionService(MindForgeDbContext db) => _db = db;

    // ── Original SM-2 (kept for backward compatibility) ───────────────────────

    public async Task<List<SpacedRepetitionItem>> GetDueItemsAsync(Guid userId)
        => await _db.SpacedRepetitionItems
            .Include(i => i.KnowledgeNode)
            .Where(i => i.UserId == userId && i.NextReviewDate <= DateTime.UtcNow)
            .OrderBy(i => i.NextReviewDate)
            .ToListAsync();

    public async Task ProcessReviewAsync(Guid itemId, int quality)
    {
        if (quality < 0 || quality > 5)
            throw new ArgumentOutOfRangeException(nameof(quality));

        var item = await _db.SpacedRepetitionItems.FindAsync(itemId);
        if (item is null) return;

        item.LastReviewDate = DateTime.UtcNow;
        item.LastQuality    = quality;

        if (quality >= 3)
        {
            item.Interval = item.Repetitions switch { 0 => 1, 1 => 6, _ => (int)Math.Round(item.Interval * item.EaseFactor) };
            item.Repetitions++;
        }
        else { item.Repetitions = 0; item.Interval = 1; }

        item.EaseFactor = Math.Max(1.3, item.EaseFactor + 0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
        item.NextReviewDate = DateTime.UtcNow.AddDays(item.Interval);
        await _db.SaveChangesAsync();
    }

    public async Task<SpacedRepetitionItem> AddItemAsync(Guid userId, Guid knowledgeNodeId)
    {
        var existing = await _db.SpacedRepetitionItems
            .FirstOrDefaultAsync(i => i.UserId == userId && i.KnowledgeNodeId == knowledgeNodeId);
        if (existing is not null) return existing;

        var item = new SpacedRepetitionItem
        {
            UserId          = userId,
            KnowledgeNodeId = knowledgeNodeId,
            EaseFactor      = 2.5,
            Interval        = 1,
            Repetitions     = 0,
            NextReviewDate  = DateTime.UtcNow
        };
        _db.SpacedRepetitionItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    // ── SM-17 algorithm ───────────────────────────────────────────────────────

    /// <summary>
    /// SM-17: grade 1=Again, 2=Hard, 3=Good, 4=Easy, 5=Perfect.
    /// Updates Difficulty, Stability, Retrievability and schedules next review.
    /// </summary>
    public async Task<SM17ReviewResult> ProcessSM17ReviewAsync(
        Guid itemId, int grade, double responseSeconds)
    {
        var item = await _db.SpacedRepetitionItems.FindAsync(itemId);
        if (item is null) throw new InvalidOperationException("Item not found.");

        var daysSince = item.LastReviewDate.HasValue
            ? (DateTime.UtcNow - item.LastReviewDate.Value).TotalDays
            : 0;

        // Current retrievability (forgetting curve)
        var stability = Math.Max(item.SM17Stability, 0.1);
        var retrievability = Math.Pow(0.9, daysSince / stability);

        // Difficulty update: blend toward actual difficulty implied by grade
        double actualDiff = (6 - grade) / 5.0;
        item.SM17Difficulty = Math.Clamp(item.SM17Difficulty * 0.8 + actualDiff * 0.2, 0.1, 0.95);

        // Stability multiplier based on grade
        double mult = grade switch
        {
            1 => 0.40,
            2 => 0.70,
            3 => 1.50,
            4 => 2.50,
            _ => 3.50   // 5 = Perfect
        };

        // Bonus for many consecutive correct reviews
        if (item.Repetitions > 10 && grade >= 3) mult *= 1.2;

        // Difficulty dampens gains
        mult *= Math.Pow(1.3 - item.SM17Difficulty, 0.5);

        item.SM17Stability = Math.Clamp(stability * mult, 1.0, 365.0);

        // Optimal next interval: when retrievability hits 85 %
        double optDays = item.SM17Stability * Math.Log(0.85) / Math.Log(0.9);
        optDays = Math.Max(1, optDays);

        item.SM17Retrievability = retrievability;
        item.LastReviewDate     = DateTime.UtcNow;
        item.NextReviewDate     = DateTime.UtcNow.AddDays(optDays);
        item.Repetitions++;
        item.LastQuality = grade;

        // Append to history JSON (keep last 20)
        var history = JsonSerializer.Deserialize<List<int>>(item.SM17HistoryJson) ?? [];
        history.Add(grade);
        if (history.Count > 20) history.RemoveAt(0);
        item.SM17HistoryJson = JsonSerializer.Serialize(history);

        await _db.SaveChangesAsync();

        return new SM17ReviewResult(
            item.NextReviewDate, item.SM17Stability,
            retrievability, item.SM17Difficulty, (int)optDays);
    }

    /// <summary>
    /// Returns up to <paramref name="max"/> items ranked by review urgency:
    /// overdue cards first, then cards with low retrievability, then by difficulty.
    /// </summary>
    public async Task<List<SpacedRepetitionItem>> GetOptimalQueueAsync(Guid userId, int max = 30)
    {
        var all = await _db.SpacedRepetitionItems
            .Include(i => i.KnowledgeNode)
            .Where(i => i.UserId == userId)
            .ToListAsync();

        var now = DateTime.UtcNow;
        return all
            .Select(item =>
            {
                var days  = item.LastReviewDate.HasValue ? (now - item.LastReviewDate.Value).TotalDays : 0;
                var stab  = Math.Max(item.SM17Stability, 0.1);
                var ret   = Math.Pow(0.9, days / stab);
                double urgency = item.NextReviewDate <= now ? 100.0 : 0.0;
                double score   = urgency + (1.0 - ret) * 50 + item.SM17Difficulty * 20;
                return (item, score);
            })
            .OrderByDescending(x => x.score)
            .Take(max)
            .Select(x => x.item)
            .ToList();
    }

    /// <summary>High-level retention summary for the dashboard.</summary>
    public async Task<RetentionSummary> GetRetentionSummaryAsync(Guid userId)
    {
        var all = await _db.SpacedRepetitionItems
            .Where(i => i.UserId == userId)
            .ToListAsync();

        if (!all.Any())
            return new RetentionSummary(0, 0, 0, 0, 0);

        var now = DateTime.UtcNow;

        double AvgRet(IEnumerable<SpacedRepetitionItem> items)
            => items.Average(i =>
            {
                var days = i.LastReviewDate.HasValue ? (now - i.LastReviewDate.Value).TotalDays : 0;
                return Math.Pow(0.9, days / Math.Max(i.SM17Stability, 0.1));
            });

        return new RetentionSummary(
            all.Count,
            all.Count(i => i.NextReviewDate <= now),
            AvgRet(all),
            all.Count(i => i.NextReviewDate.Date == now.Date.AddDays(1)),
            all.Count(i => i.NextReviewDate >= now && i.NextReviewDate <= now.AddDays(7)));
    }
}
