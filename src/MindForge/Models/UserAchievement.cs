using System;
namespace MindForge.Models;
public class UserAchievement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid AchievementId { get; set; }
    public Achievement? Achievement { get; set; }
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}
