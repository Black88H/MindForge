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

    // ── SM-17 fields ──────────────────────────────────────────────────────────
    /// <summary>Item difficulty 0–1 (higher = harder). Default 0.3.</summary>
    public double SM17Difficulty     { get; set; } = 0.3;
    /// <summary>Stability in days: how long until retention drops to 90%.</summary>
    public double SM17Stability      { get; set; } = 1.0;
    /// <summary>Current recall probability (0–1).</summary>
    public double SM17Retrievability { get; set; } = 1.0;
    /// <summary>JSON-serialized list of recent grades for trend analysis.</summary>
    public string SM17HistoryJson    { get; set; } = "[]";
}
