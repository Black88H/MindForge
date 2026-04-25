using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("LearningPlans")]
public class LearningPlan
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(1000)]          public string Description { get; set; } = string.Empty;

    public string UserId { get; set; } = "default";
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? PlannedDate { get; set; }

    public LearningPlanStatus Status { get; set; } = LearningPlanStatus.Active;
    public DifficultyLevel DifficultyLevel { get; set; } = DifficultyLevel.Mittel;

    public int DaysAvailable { get; set; } = 14;
    public int MinutesPerDay { get; set; } = 60;

    public ICollection<LearningPlanMethod> Methods { get; set; } = [];
}

public enum LearningPlanStatus { Active, Paused, Completed, Archived }
