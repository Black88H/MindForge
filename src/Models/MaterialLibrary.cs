using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("MaterialLibrary")]
public class MaterialLibrary
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)] public string UserId { get; set; } = "default";

    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }

    [Required, MaxLength(500)] public string FileName { get; set; } = string.Empty;
    public MaterialFileType FileType { get; set; } = MaterialFileType.Note;

    [MaxLength(5000)] public string ContentOrUrl { get; set; } = string.Empty;

    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
    public long SizeBytes { get; set; } = 0;
}

public enum MaterialFileType { PDF, Link, Note, Image }
