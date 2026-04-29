using MindForge.Models;
using MindForge.Services.Interfaces;
using MindForge.Helpers;
using MindForge.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

namespace MindForge.Services;

public class GamificationService : IGamificationService
{
    private readonly MindForgeDbContext _db;
    
    public GamificationService(MindForgeDbContext db)
    {
        _db = db;
    }

    public async Task AwardXPAsync(Guid userId, int amount, XPSource source, string description)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        
        var xpEvent = new XPEvent
        {
            UserId = userId,
            Amount = amount,
            Source = source,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        _db.XPEvents.Add(xpEvent);
        
        user.TotalXP += amount;
        
        // Simple Level calculation (e.g. 1 level per 1000 XP)
        int newLevel = (user.TotalXP / 1000) + 1;
        if (newLevel > user.Level)
        {
            user.Level = newLevel;
        }
        
        await _db.SaveChangesAsync();
        
        if (UserSession.UserId == userId)
        {
            UserSession.UpdateStats(user.TotalXP, user.Level, user.CurrentStreak, user.LongestStreak);
        }
    }

    public async Task UpdateStreakAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;
        
        var today = DateTime.UtcNow.Date;
        var lastLogin = user.LastLoginAt?.Date ?? today;
        
        if (lastLogin == today.AddDays(-1))
        {
            user.CurrentStreak++;
        }
        else if (lastLogin < today.AddDays(-1))
        {
            user.CurrentStreak = 1; // Reset streak
        }
        else if (user.CurrentStreak == 0) 
        {
            user.CurrentStreak = 1;
        }
        
        if (user.CurrentStreak > user.LongestStreak)
        {
            user.LongestStreak = user.CurrentStreak;
        }
        
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        
        if (UserSession.UserId == userId)
        {
            UserSession.UpdateStats(user.TotalXP, user.Level, user.CurrentStreak, user.LongestStreak);
        }
    }

    public async Task CheckBadgesAsync(Guid userId)
    {
        var user = await _db.Users
            .Include(u => u.UserBadges)
            .FirstOrDefaultAsync(u => u.Id == userId);
            
        if (user == null) return;

        var allBadges = await _db.Badges.ToListAsync();
        
        // Count materials, tests, etc.
        int uploadedMaterials = await _db.Materials.CountAsync(m => m.UserId == userId);
        int completedTests = await _db.Tests.CountAsync(t => t.UserId == userId && t.CompletedAt != null);
        int perfectTests = await _db.Tests.CountAsync(t => t.UserId == userId && t.Score == 100);
        int feynmanPassed = await _db.FeynmanSessions.CountAsync(f => f.Test != null && f.Test.UserId == userId && f.MasteryScore >= 0.8);
        int chatMessages = await _db.ChatMessages.CountAsync(c => c.UserId == userId);

        foreach (var badge in allBadges)
        {
            if (user.UserBadges.Any(ub => ub.BadgeId == badge.Id))
                continue; // Already unlocked

            bool isUnlocked = false;

            try 
            {
                var req = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(badge.Requirement);
                if (req != null && req.ContainsKey("type") && req.ContainsKey("count"))
                {
                    string type = req["type"].GetString()!;
                    int count = req["count"].GetInt32();

                    switch (type)
                    {
                        case "materials_uploaded": isUnlocked = uploadedMaterials >= count; break;
                        case "tests_completed": isUnlocked = completedTests >= count; break;
                        case "perfect_score": isUnlocked = perfectTests >= count; break;
                        case "feynman_passed": isUnlocked = feynmanPassed >= count; break;
                        case "streak": isUnlocked = user.CurrentStreak >= count; break;
                        case "chat_messages": isUnlocked = chatMessages >= count; break;
                        case "level": isUnlocked = user.Level >= count; break;
                    }
                }
            }
            catch { /* Ignore invalid JSON */ }

            if (isUnlocked)
            {
                var userBadge = new UserBadge { UserId = userId, BadgeId = badge.Id, UnlockedAt = DateTime.UtcNow };
                _db.UserBadges.Add(userBadge);
                await AwardXPAsync(userId, badge.XPReward, XPSource.AchievementUnlocked, $"Badge unlocked: {badge.Name}");
            }
        }
        
        await _db.SaveChangesAsync();
    }

    public async Task<GamificationStats> GetStatsAsync(Guid userId)
    {
        var user = await _db.Users
            .Include(u => u.UserBadges)
            .ThenInclude(ub => ub.Badge)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return new GamificationStats();

        return new GamificationStats
        {
            Level = user.Level,
            TotalXP = user.TotalXP,
            CurrentStreak = user.CurrentStreak,
            LongestStreak = user.LongestStreak,
            UnlockedBadges = user.UserBadges.Select(ub => ub.Badge!).ToList()
        };
    }
}
