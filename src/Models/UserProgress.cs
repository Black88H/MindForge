using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("UserProgress")]
public class UserProgress
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)] public string UserId { get; set; } = "default";
    public Guid? SubjectId { get; set; }

    [ForeignKey(nameof(SubjectId))]
    public Subject? Subject { get; set; }

    public int QuestionsAnswered { get; set; }
    public int CorrectAnswers { get; set; }
    public int TimeSpentMinutes { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int TotalXP { get; set; }
    public int Level { get; set; } = 1;
    public int CorrectToday { get; set; }
    public int TotalToday { get; set; }
    public DateTime LastStudied { get; set; } = DateTime.UtcNow;
    public DateTime LastStreakDate { get; set; } = DateTime.UtcNow.Date;

    [NotMapped]
    public double SuccessRate =>
        QuestionsAnswered == 0 ? 0 : (double)CorrectAnswers / QuestionsAnswered;
}
