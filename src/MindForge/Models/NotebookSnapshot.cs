using System;
namespace MindForge.Models;

/// <summary>Point-in-time snapshot of a notebook for version control.</summary>
public class NotebookSnapshot
{
    public Guid     Id            { get; set; } = Guid.NewGuid();
    public Guid     NotebookId    { get; set; }
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
    public string   Label         { get; set; } = string.Empty;
    public string   MaterialsJson { get; set; } = "[]";
    public string   ChatJson      { get; set; } = "[]";
    public int      MaterialCount { get; set; }
    public int      ChatCount     { get; set; }
}
