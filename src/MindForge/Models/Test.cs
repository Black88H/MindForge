using System;
using System.Collections.Generic;
namespace MindForge.Models;
public class Test
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string Title { get; set; } = string.Empty;
    public TestType TestSourceType { get; set; }
    public Difficulty Difficulty { get; set; }
    public int CoveragePercent { get; set; } = 50;
    public string? SourcePhotoPath { get; set; }
    public double? Score { get; set; }
    public int? TimeTakenSeconds { get; set; }
    public bool IsSkipped { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ICollection<TestQuestion> Questions { get; set; } = new List<TestQuestion>();
}
