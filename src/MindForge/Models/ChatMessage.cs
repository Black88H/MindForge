using System;
namespace MindForge.Models;
public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public int TokensUsed { get; set; } = 0;
    public string Provider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
