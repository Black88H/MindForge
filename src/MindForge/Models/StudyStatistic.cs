namespace MindForge.Models;

/// <summary>Daily aggregate learning stats per user.</summary>
public class StudyStatistic
{
    public Guid     Id                 { get; set; } = Guid.NewGuid();
    public Guid     UserId             { get; set; }
    /// <summary>Calendar date (UTC, time-part zeroed).</summary>
    public DateTime Date               { get; set; }
    public int      MinutesStudied     { get; set; }
    public int      SessionCount       { get; set; }
    public int      XPEarned           { get; set; }
    public int      FlashcardsReviewed { get; set; }
    public int      TestsTaken         { get; set; }
    public double   AverageScore       { get; set; }
    public int      QuizzesTaken       { get; set; }
    public int      ChatMessages       { get; set; }
}
