using System;
using System.Collections.Generic;

namespace MindForge.Models;

public class NotebookSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string SessionType { get; set; } = "QnA"; // QnA, Summary, StudyGuide
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SourceMaterialIds { get; set; } = string.Empty; // JSON array of Guid
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
