using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public enum XPSource
{
    TestCompleted, LessonCompleted, StreakBonus,
    AchievementUnlocked, FeynmanPassed, MaterialUploaded
}

public class XPEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    
    public int Amount { get; set; }
    public XPSource Source { get; set; }
    
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
