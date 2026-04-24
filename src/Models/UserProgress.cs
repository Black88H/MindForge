namespace MindForge.Models;

public class UserProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public int QuestionsAnswered { get; set; }
    public int CorrectAnswers { get; set; }
    public int TimeSpentMinutes { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int TotalXP { get; set; }
    public int Level { get; set; } = 1;
    public int CorrectToday { get; set; }
    public int TotalToday { get; set; }
    public DateTime LastStudied { get; set; } = DateTime.Now;
    public double SuccessRate => QuestionsAnswered == 0 ? 0 : (double)CorrectAnswers / QuestionsAnswered;
}
