using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public class TestQuestion
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TestId { get; set; }
    [ForeignKey(nameof(TestId))]
    public Test? Test { get; set; }
    
    public QuestionType QuestionType { get; set; }
    
    [Required]
    public string QuestionText { get; set; } = string.Empty;
    
    public string? Options { get; set; } // JSON-Array für MC/Matching
    
    [Required]
    public string CorrectAnswer { get; set; } = string.Empty;
    
    public string? UserAnswer { get; set; }
    public bool? IsCorrect { get; set; }
    public string? Explanation { get; set; }
    public int OrderIndex { get; set; }
}
