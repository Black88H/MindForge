using System;
namespace MindForge.Models;
public class LearningTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public LearningPlan? Plan { get; set; }
    public Guid? KnowledgeNodeId { get; set; }
    public KnowledgeNode? KnowledgeNode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public LearningTaskType TaskType { get; set; }
    public int DurationMinutes { get; set; } = 15;
    public int Priority { get; set; } = 3;
}
