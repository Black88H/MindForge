using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("Subjects")]
public class Subject
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [MaxLength(1000)]          public string Description { get; set; } = string.Empty;
    [MaxLength(10)]            public string Icon { get; set; } = "∫";
    [MaxLength(20)]            public string Color { get; set; } = "#5B8CFF";

    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Mittel;
    public double Progress { get; set; }
    public int QuestionCount { get; set; }
    public double SuccessRate { get; set; }
    [MaxLength(100)] public string LastStudied { get; set; } = string.Empty;
    public int QuestionsToday { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }

    public ICollection<Question> Questions { get; set; } = [];
    public ICollection<Test> Tests { get; set; } = [];
}
