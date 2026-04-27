using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public enum ChatRole
{
    User, Assistant
}

public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    
    public Guid? SubjectId { get; set; }
    [ForeignKey(nameof(SubjectId))]
    public Subject? Subject { get; set; }
    
    public ChatRole Role { get; set; }
    
    [Required]
    public string Content { get; set; } = string.Empty;
    
    public string? ImagePath { get; set; }
    public int TokensUsed { get; set; }
    
    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
