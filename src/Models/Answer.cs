using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("Answers")]
public class Answer
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public Guid QuestionId { get; set; }
    public Question? Question { get; set; }

    [MaxLength(1000)] public string UserAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int TimeSpentSeconds { get; set; }
    public bool HadAIExplanation { get; set; }
}
