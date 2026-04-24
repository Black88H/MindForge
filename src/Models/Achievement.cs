using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("Achievements")]
public class Achievement
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)]           public string Description { get; set; } = string.Empty;
    [MaxLength(10)]            public string Icon { get; set; } = "🏆";

    public AchievementRarity Rarity { get; set; } = AchievementRarity.Häufig;
    public bool IsUnlocked { get; set; } = false;
    public DateTime? UnlockedAt { get; set; }
    public int XpReward { get; set; } = 100;

    [MaxLength(100)] public string TriggerKey { get; set; } = string.Empty;
    public int TriggerValue { get; set; }
}

public enum AchievementRarity { Häufig, Selten, Episch, Legendär }
