using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("Tests")]
public class Test
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(300)] public string Name { get; set; } = string.Empty;

    [NotMapped]
    public Guid[] QuestionIds
    {
        get => QuestionIdsJson == null ? [] : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(QuestionIdsJson) ?? [];
        set => QuestionIdsJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    [Column("QuestionIds")] public string? QuestionIdsJson { get; set; }

    public int DurationMinutes { get; set; } = 30;
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Mittel;
    public TestType Type { get; set; } = TestType.Quiz;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public ICollection<TestResult> Results { get; set; } = [];
}

public enum TestType { Quiz, Prüfungssimulation, Schwachstellentraining }
