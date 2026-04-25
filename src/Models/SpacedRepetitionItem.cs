using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("SpacedRepetitionItems")]
public class SpacedRepetitionItem
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserProgressId { get; set; }
    public UserProgress? UserProgress { get; set; }

    public DateTime NextReviewDate { get; set; } = DateTime.UtcNow;
    public int IntervalDays { get; set; } = 1;
    public decimal EaseFactor { get; set; } = 2.5m;
    public int Repetitions { get; set; } = 0;
    public int LastQuality { get; set; } = 0;
}
