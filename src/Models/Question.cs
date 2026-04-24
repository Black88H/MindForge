namespace MindForge.Models;

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;
    public string[] Options { get; set; } = [];
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Mittel;
    public string[] Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int TimesAnswered { get; set; }
    public int TimesCorrect { get; set; }
    public double SuccessRate => TimesAnswered == 0 ? 0 : (double)TimesCorrect / TimesAnswered;
}

public enum QuestionType
{
    MultipleChoice,
    TrueFalse,
    FreeText,
    FillInTheBlank
}

public enum DifficultyLevel
{
    Leicht,
    Mittel,
    Schwer
}
