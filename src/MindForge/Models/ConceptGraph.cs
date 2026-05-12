namespace MindForge.Models;

/// <summary>AI-generated concept map for a notebook, stored as JSON.</summary>
public class ConceptGraph
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public Guid     NotebookId  { get; set; }
    /// <summary>Full JSON blob produced by AIConceptGraphService.</summary>
    public string   GraphJson   { get; set; } = "{}";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Number of concept nodes in the graph.</summary>
    public int      NodeCount   { get; set; }
}
