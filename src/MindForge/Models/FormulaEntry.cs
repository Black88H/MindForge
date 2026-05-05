using System;
namespace MindForge.Models;

/// <summary>A LaTeX formula extracted from notebook materials by the AI.</summary>
public class FormulaEntry
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public Guid   NotebookId  { get; set; }
    public string LaTeX       { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category    { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
