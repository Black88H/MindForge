using System;
using System.Threading;

namespace MindForge.Services;

public enum BgTaskStatus { Running, Completed, Canceled, Failed }

/// <summary>Live runtime descriptor for a queued background task.
/// Kept in memory while the app runs; written to DB on completion.</summary>
public class BackgroundTaskInfo
{
    public string       TaskId     { get; set; } = string.Empty;
    public string       Name       { get; set; } = string.Empty;
    public BgTaskStatus Status     { get; set; } = BgTaskStatus.Running;
    public string       Result     { get; set; } = string.Empty;
    public string       Error      { get; set; } = string.Empty;
    public DateTime     StartedAt  { get; set; } = DateTime.Now;
    public DateTime?    CompletedAt { get; set; }

    // Not bound to UI — cancellation handle for internal use only
    internal CancellationTokenSource? Cts { get; set; }

    public bool   IsRunning => Status == BgTaskStatus.Running;
    public string StatusLabel => Status switch
    {
        BgTaskStatus.Running   => "⏳ Läuft",
        BgTaskStatus.Completed => "✅ Fertig",
        BgTaskStatus.Canceled  => "⏹ Abgebrochen",
        BgTaskStatus.Failed    => "❌ Fehler",
        _                      => Status.ToString()
    };
    public string Duration => CompletedAt.HasValue
        ? $"{(CompletedAt.Value - StartedAt).TotalSeconds:F1}s"
        : $"{(DateTime.Now  - StartedAt).TotalSeconds:F1}s…";
}
