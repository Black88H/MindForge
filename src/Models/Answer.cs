namespace MindForge.Models;

public class Answer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public string UserAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int TimeSpentSeconds { get; set; }
    public bool HadAIExplanation { get; set; }
}
