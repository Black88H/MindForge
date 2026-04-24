namespace MindForge.Models;

public class TestResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TestId { get; set; }
    public double Score { get; set; }
    public int TimeSpentMinutes { get; set; }
    public Answer[] Answers { get; set; } = [];
    public DateTime CompletedAt { get; set; } = DateTime.Now;
    public int XpEarned { get; set; }
}
