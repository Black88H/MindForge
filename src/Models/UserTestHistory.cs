using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("UserTestHistory")]
public class UserTestHistory
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)] public string UserId { get; set; } = "default";

    public Guid TestId { get; set; }
    public Test? Test { get; set; }

    public double Score { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public TimeSpan TimeTaken { get; set; }
    public int Attempts { get; set; } = 1;
    public DateTime LastAttempt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<Guid> WrongAnswerIds
    {
        get => WrongAnswersJson == null ? [] : System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(WrongAnswersJson) ?? [];
        set => WrongAnswersJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    [Column("WrongAnswers")] public string? WrongAnswersJson { get; set; }
}
