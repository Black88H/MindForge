namespace MindForge.Models;

/// <summary>Individual study/timer session log.</summary>
public class StudySession
{
    public Guid      Id              { get; set; } = Guid.NewGuid();
    public Guid      UserId          { get; set; }
    public Guid?     NotebookId      { get; set; }
    /// <summary>"Pomodoro" | "FreeStudy" | "Quiz" | "Flashcard"</summary>
    public string    SessionType     { get; set; } = "Pomodoro";
    public DateTime  StartedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt         { get; set; }
    public int       DurationMinutes { get; set; }
    public bool      Completed       { get; set; }
    public int       XPEarned        { get; set; }
}
