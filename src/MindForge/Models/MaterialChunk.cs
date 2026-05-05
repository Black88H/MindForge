using System;
namespace MindForge.Models;

/// <summary>One semantic chunk of a material, optionally with an embedding vector for RAG.</summary>
public class MaterialChunk
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public Guid   MaterialId    { get; set; }
    public Guid   NotebookId    { get; set; }
    public string MaterialName  { get; set; } = string.Empty;
    public int    ChunkIndex    { get; set; }
    public string Text          { get; set; } = string.Empty;
    /// <summary>JSON-serialised float[] — "[]" when no embedding is stored.</summary>
    public string EmbeddingJson { get; set; } = "[]";
}
