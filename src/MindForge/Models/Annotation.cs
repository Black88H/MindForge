namespace MindForge.Models;

public enum AnnotationType { Highlight, Important, Question, Concept, Example, Todo, Confusion }

public class Annotation
{
    public Guid   Id         { get; set; } = Guid.NewGuid();
    public Guid   MaterialId { get; set; }
    public Guid   UserId     { get; set; }
    public string SelectedText { get; set; } = string.Empty;
    public AnnotationType Type { get; set; } = AnnotationType.Highlight;
    /// <summary>Hex colour (#RRGGBB) depending on type.</summary>
    public string Color     { get; set; } = "#FBBF24";
    /// <summary>AI-generated smart note (for Question / Concept types).</summary>
    public string AINote    { get; set; } = string.Empty;
    public string UserNote  { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int    PageNumber { get; set; }
}
