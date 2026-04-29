using System;
namespace MindForge.Models;
public class Material
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public MaterialFormat OriginalFormat { get; set; }
    public string OriginalFilePath { get; set; } = string.Empty;
    public string KiContent { get; set; } = string.Empty;
    public string KiContentHash { get; set; } = string.Empty;
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public int TokenCount { get; set; }
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
