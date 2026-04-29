using System;
using System.Collections.Generic;
namespace MindForge.Models;
public class Subject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string IconKey { get; set; } = "book";
    public string Color { get; set; } = "#6366F1";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Material> Materials { get; set; } = new List<Material>();
    public ICollection<KnowledgeNode> KnowledgeNodes { get; set; } = new List<KnowledgeNode>();
    public ICollection<Test> Tests { get; set; } = new List<Test>();
}
