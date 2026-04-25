using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("LearningMethods")]
public class LearningMethod
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(500)]           public string Description { get; set; } = string.Empty;
    [MaxLength(20)]            public string Icon { get; set; } = "📚";

    public LearningMethodType Type { get; set; } = LearningMethodType.ActiveRecall;

    public ICollection<LearningPlanMethod> PlanMethods { get; set; } = [];
}

public enum LearningMethodType
{
    ActiveRecall,
    Pomodoro,
    SpacedRepetition,
    Interleaving,
    PracticeTest
}
