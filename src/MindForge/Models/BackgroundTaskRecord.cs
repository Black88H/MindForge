using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

/// <summary>EF entity — persists completed background task results to SQLite.</summary>
[Table("BackgroundTaskRecords")]
public class BackgroundTaskRecord
{
    [Key] public Guid     Id          { get; set; } = Guid.NewGuid();
    public string         TaskName    { get; set; } = string.Empty;
    public string         Status      { get; set; } = string.Empty;
    public string         Result      { get; set; } = string.Empty;
    public string         Error       { get; set; } = string.Empty;
    public DateTime       StartedAt   { get; set; }
    public DateTime?      CompletedAt { get; set; }
}
