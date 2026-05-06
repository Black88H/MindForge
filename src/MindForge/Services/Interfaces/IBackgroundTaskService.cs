using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public interface IBackgroundTaskService
{
    /// <summary>Queue <paramref name="taskFunc"/> to run on a background thread.
    /// Returns a short task-ID string you can use to poll / cancel the task.</summary>
    Task<string> QueueAsync(Func<CancellationToken, Task<string>> taskFunc, string taskName);

    /// <summary>Request cancellation of a running task.</summary>
    void CancelTask(string taskId);

    /// <summary>Look up a single task by ID (or null if unknown).</summary>
    BackgroundTaskInfo? GetTask(string taskId);

    /// <summary>Snapshot of all tracked tasks (running + finished).</summary>
    IReadOnlyList<BackgroundTaskInfo> GetAllTasks();

    /// <summary>Live collection suitable for UI binding (updated on the UI dispatcher).</summary>
    ObservableCollection<BackgroundTaskInfo> Tasks { get; }

    /// <summary>Fires on the UI thread when a task completes successfully.</summary>
    event EventHandler<BackgroundTaskInfo>? TaskCompleted;

    /// <summary>Fires on the UI thread when a task fails or is cancelled.</summary>
    event EventHandler<BackgroundTaskInfo>? TaskFailed;
}
