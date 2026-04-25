using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("Challenges")]
public class Challenge
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(500)]           public string Description { get; set; } = string.Empty;
    [MaxLength(20)]            public string Icon { get; set; } = "🎯";

    public ChallengeType Type { get; set; } = ChallengeType.Daily;
    public int XpReward { get; set; } = 100;
    public ChallengeStatus Status { get; set; } = ChallengeStatus.Active;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresDate { get; set; }
    public int RequiredProgress { get; set; } = 1;

    public ICollection<UserChallenge> UserChallenges { get; set; } = [];
}

public enum ChallengeType { Daily, Custom, Weekly }
public enum ChallengeStatus { Active, Expired, Completed }
