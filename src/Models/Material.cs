using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public enum MaterialFormat
{
    PDF, DOCX, Image, Handwriting
}

public class Material
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid SubjectId { get; set; }
    [ForeignKey(nameof(SubjectId))]
    public Subject? Subject { get; set; }
    
    public Guid UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    
    [Required, MaxLength(500)]
    public string OriginalFileName { get; set; } = string.Empty;
    
    public MaterialFormat OriginalFormat { get; set; }
    
    [Required]
    public string OriginalFilePath { get; set; } = string.Empty;
    
    [Required]
    public string KiContent { get; set; } = string.Empty;
    
    [MaxLength(64)]
    public string KiContentHash { get; set; } = string.Empty;
    
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public int TokenCount { get; set; }
    
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
