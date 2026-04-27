using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("SpacedRepetitionItems")]
public class SpacedRepetitionItem
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid KnowledgeNodeId { get; set; }
    [ForeignKey(nameof(KnowledgeNodeId))]
    public KnowledgeNode? KnowledgeNode { get; set; }

    public Guid UserProgressId { get; set; }
    public UserProgress? UserProgress { get; set; }

    public float EaseFactor { get; set; } = 2.5f;
    public int Interval { get; set; } = 1; // Tage
    public int IntervalDays { get => Interval; set => Interval = value; } // backwards compatibility
    
    public int Repetitions { get; set; } = 0;
    
    public DateTime NextReviewDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastReviewDate { get; set; }
    public int LastQuality { get; set; } = 0; // 0-5
}
