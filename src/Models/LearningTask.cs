using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public enum LearningTaskType
{
    Review, NewContent, Test, FeynmanCheck
}

public class LearningTask
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid PlanId { get; set; }
    [ForeignKey(nameof(PlanId))]
    public LearningPlan? Plan { get; set; }
    
    public Guid? KnowledgeNodeId { get; set; }
    [ForeignKey(nameof(KnowledgeNodeId))]
    public KnowledgeNode? KnowledgeNode { get; set; }
    
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;
    
    public string Description { get; set; } = string.Empty;
    
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public LearningTaskType TaskType { get; set; }
    public int DurationMinutes { get; set; } = 15;
    public int Priority { get; set; } = 3; // 1-5
}
