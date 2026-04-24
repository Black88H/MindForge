using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services.AI.Interfaces;

namespace MindForge.Services;

public class TokenTrackerService : ITokenTracker
{
    private readonly MindForgeDbContext _db;

    public TokenTrackerService(MindForgeDbContext db) => _db = db;

    public async Task TrackUsageAsync(string provider, string taskType, int tokens, double cost)
    {
        _db.TokenUsage.Add(new TokenUsage
        {
            Provider   = provider,
            TaskType   = taskType,
            TokensUsed = tokens,
            CostUSD    = cost,
        });
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetTotalTokensAsync(int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await _db.TokenUsage.Where(t => t.Timestamp >= since).SumAsync(t => t.TokensUsed);
    }

    public async Task<double> GetTotalCostAsync(int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await _db.TokenUsage.Where(t => t.Timestamp >= since).SumAsync(t => t.CostUSD);
    }
}
