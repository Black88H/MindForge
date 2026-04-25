using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

[Table("OCRDocuments")]
public class OCRDocument
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)] public string UserId { get; set; } = "default";

    [MaxLength(1000)] public string UploadedFilePath { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;

    public int Confidence { get; set; } = 0;
    public OCRStatus Status { get; set; } = OCRStatus.Processing;
    public DateTime ProcessedDate { get; set; } = DateTime.UtcNow;
}

public enum OCRStatus { Processing, Success, PartialFail, Failed }
