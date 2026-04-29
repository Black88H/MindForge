using System;
namespace MindForge.Models;
public class FeynmanSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TestId { get; set; }
    public Test? Test { get; set; }
    public Guid KnowledgeNodeId { get; set; }
    public KnowledgeNode? KnowledgeNode { get; set; }
    public string UserExplanation { get; set; } = string.Empty;
    public string AiAssessment { get; set; } = string.Empty;
    public string GapsIdentified { get; set; } = string.Empty;
    public double MasteryScore { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
