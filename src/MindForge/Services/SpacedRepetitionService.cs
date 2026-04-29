using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class SpacedRepetitionService : ISpacedRepetitionService
{
    private readonly MindForgeDbContext _db;

    public SpacedRepetitionService(MindForgeDbContext db)
    {
        _db = db;
    }

    public async Task<List<SpacedRepetitionItem>> GetDueItemsAsync(Guid userId)
    {
        return await _db.SpacedRepetitionItems
            .Include(i => i.KnowledgeNode)
            .Where(i => i.UserId == userId && i.NextReviewDate <= DateTime.UtcNow)
            .OrderBy(i => i.NextReviewDate)
            .ToListAsync();
    }

    public async Task ProcessReviewAsync(Guid itemId, int quality)
    {
        if (quality < 0 || quality > 5)
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 0 and 5.");

        var item = await _db.SpacedRepetitionItems.FindAsync(itemId);
        if (item == null) return;

        item.LastReviewDate = DateTime.UtcNow;
        item.LastQuality = quality;

        if (quality >= 3)
        {
            if (item.Repetitions == 0)
            {
                item.Interval = 1;
            }
            else if (item.Repetitions == 1)
            {
                item.Interval = 6;
            }
            else
            {
                item.Interval = (int)Math.Round(item.Interval * item.EaseFactor);
            }
            item.Repetitions++;
        }
        else
        {
            item.Repetitions = 0;
            item.Interval = 1;
        }

        item.EaseFactor = item.EaseFactor + (0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
        if (item.EaseFactor < 1.3)
        {
            item.EaseFactor = 1.3;
        }

        item.NextReviewDate = DateTime.UtcNow.AddDays(item.Interval);
        
        await _db.SaveChangesAsync();
    }

    public async Task<SpacedRepetitionItem> AddItemAsync(Guid userId, Guid knowledgeNodeId)
    {
        var existing = await _db.SpacedRepetitionItems.FirstOrDefaultAsync(i => i.UserId == userId && i.KnowledgeNodeId == knowledgeNodeId);
        if (existing != null) return existing;

        var item = new SpacedRepetitionItem
        {
            UserId = userId,
            KnowledgeNodeId = knowledgeNodeId,
            EaseFactor = 2.5,
            Interval = 1,
            Repetitions = 0,
            NextReviewDate = DateTime.UtcNow,
            LastQuality = 0
        };

        _db.SpacedRepetitionItems.Add(item);
        await _db.SaveChangesAsync();
        return item;
    }
}
