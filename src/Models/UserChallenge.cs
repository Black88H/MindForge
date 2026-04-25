using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("UserChallenges")]
public class UserChallenge
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)] public string UserId { get; set; } = "default";

    public Guid ChallengeId { get; set; }
    public Challenge? Challenge { get; set; }

    public int Progress { get; set; } = 0;
    public bool Completed { get; set; } = false;
    public DateTime? CompletedDate { get; set; }
}
