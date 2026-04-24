using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("Questions")]
public class Question
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(2000)]
    public string Text { get; set; } = string.Empty;

    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;

    [NotMapped]
    public string[] Options
    {
        get => OptionsJson == null ? [] : System.Text.Json.JsonSerializer.Deserialize<string[]>(OptionsJson) ?? [];
        set => OptionsJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    [Column("Options")] public string? OptionsJson { get; set; }

    [MaxLength(1000)] public string CorrectAnswer { get; set; } = string.Empty;
    [MaxLength(2000)] public string Explanation { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Mittel;

    [NotMapped]
    public string[] Tags
    {
        get => TagsJson == null ? [] : System.Text.Json.JsonSerializer.Deserialize<string[]>(TagsJson) ?? [];
        set => TagsJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    [Column("Tags")] public string? TagsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int TimesAnswered { get; set; }
    public int TimesCorrect { get; set; }

    [NotMapped]
    public double SuccessRate => TimesAnswered == 0 ? 0 : (double)TimesCorrect / TimesAnswered;

    public ICollection<Answer> Answers { get; set; } = [];
}

public enum QuestionType { MultipleChoice, TrueFalse, FreeText, FillInTheBlank }
public enum DifficultyLevel { Leicht, Mittel, Schwer }
