using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public interface ISpacedRepetitionService
{
    Task<SpacedRepetitionItem> ScheduleReviewAsync(Guid userId, Guid knowledgeNodeId);
    Task<List<SpacedRepetitionItem>> GetDueReviewsAsync(Guid userId);
    Task RecordReviewAsync(Guid itemId, int quality);
}

public class SpacedRepetitionService : ISpacedRepetitionService
{
    private readonly MindForgeDbContext _db;

    public SpacedRepetitionService(MindForgeDbContext db) => _db = db;

    public async Task<SpacedRepetitionItem> ScheduleReviewAsync(Guid userId, Guid knowledgeNodeId)
    {
        var existing = await _db.Set<SpacedRepetitionItem>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.KnowledgeNodeId == knowledgeNodeId);

        if (existing != null) return existing;

        var item = new SpacedRepetitionItem
        {
            UserId = userId,
            KnowledgeNodeId = knowledgeNodeId,
            EaseFactor = 2.5f,
            Interval = 1,
            Repetitions = 0,
            NextReviewDate = DateTime.UtcNow.Date.AddDays(1)
        };

        _db.Set<SpacedRepetitionItem>().Add(item);
        await _db.SaveChangesAsync();
        return item;
    }

    public async Task<List<SpacedRepetitionItem>> GetDueReviewsAsync(Guid userId)
    {
        return await _db.Set<SpacedRepetitionItem>()
            .Include(s => s.KnowledgeNode)
            .Where(s => s.UserId == userId && s.NextReviewDate <= DateTime.UtcNow.Date)
            .OrderBy(s => s.NextReviewDate)
            .ToListAsync();
    }

    public async Task RecordReviewAsync(Guid itemId, int quality)
    {
        quality = Math.Clamp(quality, 0, 5);

        var item = await _db.Set<SpacedRepetitionItem>().FindAsync(itemId)
            ?? throw new ArgumentException("Item nicht gefunden");

        // SM-2 Algorithmus
        item.LastQuality = quality;
        item.LastReviewDate = DateTime.UtcNow;

        if (quality >= 3)
        {
            item.Repetitions++;
            item.Interval = item.Repetitions switch
            {
                1 => 1,
                2 => 6,
                _ => (int)Math.Round(item.Interval * item.EaseFactor)
            };
        }
        else
        {
            item.Repetitions = 0;
            item.Interval = 1;
        }

        // EaseFactor anpassen
        var ef = item.EaseFactor + (0.1f - (5 - quality) * (0.08f + (5 - quality) * 0.02f));
        item.EaseFactor = Math.Max(1.3f, ef);

        item.NextReviewDate = DateTime.UtcNow.Date.AddDays(item.Interval);

        await _db.SaveChangesAsync();
    }
}
