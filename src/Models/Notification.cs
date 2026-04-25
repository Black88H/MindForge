using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("Notifications")]
public class Notification
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]  public string UserId { get; set; } = "default";
    [Required, MaxLength(1000)] public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.Info;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Read { get; set; } = false;
    public int? XpAmount { get; set; }
}

public enum NotificationType { Info, Warning, Success, XPEarned }
