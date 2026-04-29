using MindForge.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public interface IGamificationService
{
    Task AwardXPAsync(Guid userId, int amount, XPSource source, string description);
    Task UpdateStreakAsync(Guid userId);
    Task CheckBadgesAsync(Guid userId);
    Task<GamificationStats> GetStatsAsync(Guid userId);
}

public class GamificationStats
{
    public int Level { get; set; }
    public int TotalXP { get; set; }
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
    public List<Badge> UnlockedBadges { get; set; } = new();
}
