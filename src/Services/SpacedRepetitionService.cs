using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class SpacedRepetitionService
{
    private readonly MindForgeDbContext _db;

    public SpacedRepetitionService(MindForgeDbContext db) => _db = db;

    // SM-2 algorithm
    public DateTime CalculateNextReview(SpacedRepetitionItem item, int quality)
    {
        quality = Math.Clamp(quality, 0, 5);

        if (quality < 3)
        {
            item.IntervalDays = 1;
            item.EaseFactor = Math.Max(1.3m, item.EaseFactor + 0.6m - 5 * (0.08m + (5 - quality) * 0.02m));
        }
        else
        {
            item.IntervalDays = item.Repetitions == 0 ? 1
                               : item.Repetitions == 1 ? 6
                               : (int)Math.Round((double)(item.IntervalDays * item.EaseFactor));
            item.EaseFactor += 0.1m - (5 - quality) * (0.08m + (5 - quality) * 0.02m);
            item.EaseFactor = Math.Max(1.3m, item.EaseFactor);
        }

        item.LastQuality = quality;
        item.Repetitions++;
        item.NextReviewDate = DateTime.UtcNow.AddDays(item.IntervalDays);
        return item.NextReviewDate;
    }

    public async Task<List<UserProgress>> GetDueItemsAsync(string userId)
    {
        var today = DateTime.UtcNow.Date;
        var dueProgressIds = await _db.SpacedRepetitionItems
            .Where(sri => sri.NextReviewDate.Date <= today)
            .Select(sri => sri.UserProgressId)
            .ToListAsync();

        return await _db.UserProgress
            .Include(p => p.Subject)
            .Where(p => p.UserId == userId && dueProgressIds.Contains(p.Id))
            .ToListAsync();
    }

    public async Task UpdateReviewAsync(Guid userProgressId, int quality)
    {
        var item = await _db.SpacedRepetitionItems
            .FirstOrDefaultAsync(sri => sri.UserProgressId == userProgressId);

        if (item == null)
        {
            item = new SpacedRepetitionItem { UserProgressId = userProgressId };
            _db.SpacedRepetitionItems.Add(item);
        }

        CalculateNextReview(item, quality);
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetDueCountAsync(string userId)
    {
        var today = DateTime.UtcNow.Date;
        var progressIds = await _db.UserProgress
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .ToListAsync();

        return await _db.SpacedRepetitionItems
            .CountAsync(sri => progressIds.Contains(sri.UserProgressId) && sri.NextReviewDate.Date <= today);
    }

    public async Task<List<(DateTime Date, int Count)>> GetUpcomingReviewsAsync(string userId, int days = 7)
    {
        var progressIds = await _db.UserProgress
            .Where(p => p.UserId == userId)
            .Select(p => p.Id)
            .ToListAsync();

        var upcoming = await _db.SpacedRepetitionItems
            .Where(sri => progressIds.Contains(sri.UserProgressId) &&
                          sri.NextReviewDate.Date >= DateTime.UtcNow.Date &&
                          sri.NextReviewDate.Date <= DateTime.UtcNow.AddDays(days).Date)
            .GroupBy(sri => sri.NextReviewDate.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return upcoming.Select(x => (x.Date, x.Count)).ToList();
    }
}
