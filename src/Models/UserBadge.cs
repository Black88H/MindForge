using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public class UserBadge
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    
    public Guid BadgeId { get; set; }
    [ForeignKey(nameof(BadgeId))]
    public Badge? Badge { get; set; }
    
    public DateTime UnlockedAt { get; set; } = DateTime.UtcNow;
}
