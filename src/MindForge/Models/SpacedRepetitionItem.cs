using System;
namespace MindForge.Models;
public class SpacedRepetitionItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid KnowledgeNodeId { get; set; }
    public KnowledgeNode? KnowledgeNode { get; set; }
    public double EaseFactor { get; set; } = 2.5;
    public int Interval { get; set; } = 1;
    public int Repetitions { get; set; } = 0;
    public DateTime NextReviewDate { get; set; }
    public DateTime? LastReviewDate { get; set; }
    public int LastQuality { get; set; } = 0;
}
