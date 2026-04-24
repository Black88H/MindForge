namespace MindForge.Models;

public class Achievement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "🏆";
    public AchievementRarity Rarity { get; set; } = AchievementRarity.Häufig;
    public bool IsUnlocked { get; set; } = false;
    public DateTime? UnlockedAt { get; set; }
    public int XpReward { get; set; } = 100;
}

public enum AchievementRarity
{
    Häufig,
    Selten,
    Episch,
    Legendär
}
