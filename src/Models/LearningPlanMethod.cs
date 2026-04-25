using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("LearningPlanMethods")]
public class LearningPlanMethod
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LearningPlanId { get; set; }
    public LearningPlan? LearningPlan { get; set; }

    public Guid LearningMethodId { get; set; }
    public LearningMethod? LearningMethod { get; set; }

    public int Order { get; set; } = 0;
    public int DailyMinutes { get; set; } = 30;
}
