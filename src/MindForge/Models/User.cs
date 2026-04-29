using System;
using System.Collections.Generic;
namespace MindForge.Models;
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int TotalXP { get; set; } = 0;
    public int Level { get; set; } = 1;
    public int CurrentStreak { get; set; } = 0;
    public int LongestStreak { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public ICollection<Subject> Subjects { get; set; } = new List<Subject>();
    public ICollection<Material> Materials { get; set; } = new List<Material>();
    public ICollection<Test> Tests { get; set; } = new List<Test>();
    public ICollection<XPEvent> XPEvents { get; set; } = new List<XPEvent>();
    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}
