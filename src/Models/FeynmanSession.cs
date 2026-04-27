using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public class FeynmanSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid TestId { get; set; }
    [ForeignKey(nameof(TestId))]
    public Test? Test { get; set; }
    
    public Guid KnowledgeNodeId { get; set; }
    [ForeignKey(nameof(KnowledgeNodeId))]
    public KnowledgeNode? KnowledgeNode { get; set; }
    
    public string UserExplanation { get; set; } = string.Empty;
    public string AiAssessment { get; set; } = string.Empty;
    public string GapsIdentified { get; set; } = string.Empty; // JSON-Array
    public float MasteryScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
