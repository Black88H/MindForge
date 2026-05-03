using System;
namespace MindForge.Models;

/// <summary>Per-subject notebook — persisted in the Notebooks SQLite table.</summary>
public class Notebook
{
    public Guid   Id               { get; set; } = Guid.NewGuid();
    public Guid   SubjectId        { get; set; }
    public Guid   UserId           { get; set; }
    public string Name             { get; set; } = string.Empty;
    public string LearningLevel    { get; set; } = "Fortgeschritten";
    public string ExplanationStyle { get; set; } = "Normal";
    public double Progress         { get; set; }
    public int    ChatCount        { get; set; }
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime LastModified   { get; set; } = DateTime.UtcNow;
}
