using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("TestResults")]
public class TestResult
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TestId { get; set; }
    public Test? Test { get; set; }

    public double Score { get; set; }
    public int TimeSpentMinutes { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    public int XpEarned { get; set; }
    public int CorrectCount { get; set; }
    public int TotalCount { get; set; }
}
