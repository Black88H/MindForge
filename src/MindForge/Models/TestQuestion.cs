using System;
namespace MindForge.Models;
public class TestQuestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TestId { get; set; }
    public Test? Test { get; set; }
    public QuestionType QuestionType { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Options { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public string? UserAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public string? Explanation { get; set; }
    public int OrderIndex { get; set; }
}
