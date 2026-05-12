namespace MindForge.Models;

/// <summary>Flattened full-text search record written whenever content changes.</summary>
public class SearchIndex
{
    public Guid     Id         { get; set; } = Guid.NewGuid();
    /// <summary>"Notebook" | "Material" | "Chat"</summary>
    public string   EntityType { get; set; } = string.Empty;
    public Guid     EntityId   { get; set; }
    public string   Title      { get; set; } = string.Empty;
    /// <summary>Plain-text snippet (first 500 chars of content).</summary>
    public string   Snippet    { get; set; } = string.Empty;
    public Guid     UserId     { get; set; }
    public DateTime IndexedAt  { get; set; } = DateTime.UtcNow;
}
