using System;
using System.Collections.Generic;
namespace MindForge.Models;
public class LearningPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? GoalDate { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<LearningTask> Tasks { get; set; } = new List<LearningTask>();
}
