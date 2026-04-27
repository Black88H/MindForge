using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public class KnowledgeNode
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    
    public Guid SubjectId { get; set; }
    [ForeignKey(nameof(SubjectId))]
    public Subject? Subject { get; set; }
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    public string Summary { get; set; } = string.Empty;
    
    public string MaterialIds { get; set; } = string.Empty; // Kommaseparierte Guids
    
    public float MasteryLevel { get; set; } = 0f; // 0.0 - 1.0
    
    public DateTime? LastReviewed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<KnowledgeEdge> OutgoingEdges { get; set; } = new List<KnowledgeEdge>();
    public ICollection<KnowledgeEdge> IncomingEdges { get; set; } = new List<KnowledgeEdge>();
}
