using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public enum TestType { Generated, FromPhoto, Custom, Quiz, Prüfungssimulation, Schwachstellentraining }
public enum Difficulty { Easy, Medium, Hard, Exam }

[Table("Tests")]
public class Test
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Required, MaxLength(300)] public string Name { get; set; } = string.Empty;
    public string Title { get => Name; set => Name = value; }

    [NotMapped]
    public Guid[] QuestionIds
    {
        get => QuestionIdsJson == null ? [] : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(QuestionIdsJson) ?? [];
        set => QuestionIdsJson = System.Text.Json.JsonSerializer.Serialize(value);
    }

    [Column("QuestionIds")] public string? QuestionIdsJson { get; set; }

    public int DurationMinutes { get; set; } = 30;
    
    public Difficulty Difficulty { get; set; } = Difficulty.Medium;
    public TestType Type { get; set; } = TestType.Quiz;
    public TestType TestSourceType { get; set; } = TestType.Generated;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    public ICollection<TestResult> Results { get; set; } = new List<TestResult>();

    public int CoveragePercent { get; set; } = 50;
    public string? SourcePhotoPath { get; set; }
    public float? Score { get; set; }
    public int? TimeTakenSeconds { get; set; }
    public bool IsSkipped { get; set; } = false;

    public ICollection<TestQuestion> Questions { get; set; } = new List<TestQuestion>();
    public ICollection<FeynmanSession> FeynmanSessions { get; set; } = new List<FeynmanSession>();
}
