namespace MindForge.Models;

public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "∫";
    public string Color { get; set; } = "#5B8CFF";
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Mittel;
    public double Progress { get; set; }
    public int QuestionCount { get; set; }
    public double SuccessRate { get; set; }
    public string LastStudied { get; set; } = string.Empty;
    public int QuestionsToday { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
