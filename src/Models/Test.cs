namespace MindForge.Models;

public class Test
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid[] QuestionIds { get; set; } = [];
    public int DurationMinutes { get; set; } = 30;
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Mittel;
    public TestType Type { get; set; } = TestType.Quiz;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public Guid SubjectId { get; set; }
}

public enum TestType
{
    Quiz,
    Prüfungssimulation,
    Schwachstellentraining
}
