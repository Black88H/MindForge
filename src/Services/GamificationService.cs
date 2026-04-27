using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public interface IGamificationService
{
    Task<XPEvent> AwardXPAsync(Guid userId, int amount, XPSource source, string description);
    Task<(bool streakContinued, int currentStreak)> UpdateStreakAsync(Guid userId);
    Task<List<Badge>> CheckBadgesAsync(Guid userId);
    Task<GamificationStats> GetStatsAsync(Guid userId);
    int GetLevelForXP(int totalXP);
    int GetXPForLevel(int level);
}

public record GamificationStats(int Level, int TotalXP, int XPToNextLevel, int CurrentStreak, int LongestStreak, List<Badge> Badges, List<Achievement> Achievements);

public class GamificationService : IGamificationService
{
    private readonly MindForgeDbContext _db;

    public GamificationService(MindForgeDbContext db) => _db = db;

    public async Task<XPEvent> AwardXPAsync(Guid userId, int amount, XPSource source, string description)
    {
        var user = await _db.Set<User>().FindAsync(userId)
            ?? throw new ArgumentException("User nicht gefunden");

        var evt = new XPEvent
        {
            UserId = userId,
            Amount = amount,
            Source = source,
            Description = description
        };
        _db.XPEvents.Add(evt);

        user.TotalXP += amount;

        // Level-Check
        var newLevel = GetLevelForXP(user.TotalXP);
        if (newLevel > user.Level)
            user.Level = newLevel;

        await _db.SaveChangesAsync();
        return evt;
    }

    public async Task<(bool streakContinued, int currentStreak)> UpdateStreakAsync(Guid userId)
    {
        var user = await _db.Set<User>().FindAsync(userId)
            ?? throw new ArgumentException("User nicht gefunden");

        var lastActivity = await _db.XPEvents
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.CreatedAt.Date)
            .FirstOrDefaultAsync();

        var today = DateTime.UtcNow.Date;

        if (lastActivity == today)
            return (true, user.CurrentStreak); // Heute bereits aktiv

        if (lastActivity == today.AddDays(-1))
        {
            user.CurrentStreak++;
            if (user.CurrentStreak > user.LongestStreak)
                user.LongestStreak = user.CurrentStreak;

            // Streak-Bonus XP
            if (user.CurrentStreak == 7)
                await AwardXPAsync(userId, 50, XPSource.StreakBonus, "7-Tage-Streak!");
            else if (user.CurrentStreak == 30)
                await AwardXPAsync(userId, 200, XPSource.StreakBonus, "30-Tage-Streak!");
            else if (user.CurrentStreak == 100)
                await AwardXPAsync(userId, 500, XPSource.StreakBonus, "100-Tage-Streak!");

            await _db.SaveChangesAsync();
            return (true, user.CurrentStreak);
        }
        else
        {
            user.CurrentStreak = 1;
            await _db.SaveChangesAsync();
            return (false, 1);
        }
    }

    public async Task<List<Badge>> CheckBadgesAsync(Guid userId)
    {
        var user = await _db.Set<User>().FindAsync(userId);
        if (user == null) return new();

        var existingBadgeIds = await _db.UserBadges
            .Where(ub => ub.UserId == userId)
            .Select(ub => ub.BadgeId)
            .ToListAsync();

        var allBadges = await _db.Badges.ToListAsync();
        var newBadges = new List<Badge>();

        foreach (var badge in allBadges.Where(b => !existingBadgeIds.Contains(b.Id)))
        {
            try
            {
                var req = JsonSerializer.Deserialize<BadgeRequirement>(badge.Requirement,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (req == null) continue;

                var met = req.Type switch
                {
                    "materials_uploaded" => await _db.Materials.CountAsync(m => m.UserId == userId) >= req.Count,
                    "tests_completed" => await _db.Set<Test>().CountAsync(t => t.UserId == userId && t.CompletedAt != null) >= req.Count,
                    "perfect_score" => await _db.Set<Test>().AnyAsync(t => t.UserId == userId && t.Score >= 1.0f),
                    "feynman_passed" => await _db.FeynmanSessions.CountAsync(f => f.MasteryScore >= 0.7f) >= req.Count,
                    "streak" => user.LongestStreak >= req.Count,
                    "chat_messages" => await _db.ChatMessages.CountAsync(m => m.UserId == userId && m.Role == ChatRole.User) >= req.Count,
                    "plans_created" => await _db.Set<LearningPlan>().CountAsync(p => p.UserId == userId) >= req.Count,
                    "level" => user.Level >= req.Count,
                    _ => false
                };

                if (met)
                {
                    _db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = badge.Id });
                    await AwardXPAsync(userId, badge.XPReward, XPSource.AchievementUnlocked, $"Badge: {badge.Name}");
                    newBadges.Add(badge);
                }
            }
            catch { /* Requirement-Format ungültig, überspringen */ }
        }

        if (newBadges.Any())
            await _db.SaveChangesAsync();

        return newBadges;
    }

    public async Task<GamificationStats> GetStatsAsync(Guid userId)
    {
        var user = await _db.Set<User>().FindAsync(userId);
        if (user == null) return new(0, 0, 100, 0, 0, new(), new());

        var badges = await _db.UserBadges
            .Where(ub => ub.UserId == userId)
            .Include(ub => ub.Badge)
            .Select(ub => ub.Badge!)
            .ToListAsync();

        var achievements = await _db.Set<Achievement>().ToListAsync(); // TODO: filtern nach User

        var nextLevelXP = GetXPForLevel(user.Level + 1);
        var xpToNext = nextLevelXP - user.TotalXP;

        return new GamificationStats(
            user.Level, user.TotalXP, Math.Max(0, xpToNext),
            user.CurrentStreak, user.LongestStreak,
            badges, achievements);
    }

    public int GetLevelForXP(int totalXP)
    {
        int level = 1;
        while (GetXPForLevel(level + 1) <= totalXP)
            level++;
        return level;
    }

    public int GetXPForLevel(int level)
    {
        return (int)Math.Round(100 * Math.Pow(level, 1.5));
    }

    private record BadgeRequirement(string Type, int Count);
}
