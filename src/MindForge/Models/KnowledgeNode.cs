using System;
using System.Collections.Generic;
namespace MindForge.Models;
public class KnowledgeNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? MaterialIds { get; set; }
    public double MasteryLevel { get; set; } = 0.0;
    public DateTime? LastReviewed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
    public Subject? Subject { get; set; }
    public ICollection<KnowledgeEdge> OutgoingEdges { get; set; } = new List<KnowledgeEdge>();
    public ICollection<KnowledgeEdge> IncomingEdges { get; set; } = new List<KnowledgeEdge>();
}
