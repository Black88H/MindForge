using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("TokenUsage")]
public class TokenUsage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [MaxLength(50)]
    public string TaskType { get; set; } = string.Empty;

    public int TokensUsed { get; set; }
    public double CostUSD { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
