using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

/// <summary>
/// Singleton service that queues AI/processing tasks onto background threads,
/// tracks their status, and raises events on the UI dispatcher when they finish.
/// Uses IServiceScopeFactory for all DB access (avoids Scoped-in-Singleton issue).
/// </summary>
public class BackgroundTaskService : IBackgroundTaskService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, BackgroundTaskInfo> _tasks = new();
    private readonly object _uiLock = new();

    public ObservableCollection<BackgroundTaskInfo> Tasks { get; } = new();

    public event EventHandler<BackgroundTaskInfo>? TaskCompleted;
    public event EventHandler<BackgroundTaskInfo>? TaskFailed;

    public BackgroundTaskService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // ── Queue ─────────────────────────────────────────────────────────────────

    public Task<string> QueueAsync(
        Func<CancellationToken, Task<string>> taskFunc,
        string taskName)
    {
        var taskId = Guid.NewGuid().ToString("N")[..8];
        var cts    = new CancellationTokenSource();

        var info = new BackgroundTaskInfo
        {
            TaskId    = taskId,
            Name      = taskName,
            Status    = BgTaskStatus.Running,
            StartedAt = DateTime.Now,
            Cts       = cts
        };

        _tasks[taskId] = info;
        UiAdd(info);

        // Fire-and-forget on the thread pool; exception handling inside
        _ = Task.Run(async () =>
        {
            try
            {
                info.Result = await taskFunc(cts.Token);
                info.Status = BgTaskStatus.Completed;
            }
            catch (OperationCanceledException)
            {
                info.Status = BgTaskStatus.Canceled;
            }
            catch (Exception ex)
            {
                info.Status = BgTaskStatus.Failed;
                info.Error  = ex.Message;
                App.LogError(ex);
            }
            finally
            {
                info.CompletedAt = DateTime.Now;
                UiRefresh(info);
                RaiseEvent(info);
                await PersistAsync(info);
            }
        }, cts.Token);

        return Task.FromResult(taskId);
    }

    // ── Control ───────────────────────────────────────────────────────────────

    public void CancelTask(string taskId)
    {
        if (_tasks.TryGetValue(taskId, out var info))
            info.Cts?.Cancel();
    }

    public BackgroundTaskInfo? GetTask(string taskId)
        => _tasks.TryGetValue(taskId, out var info) ? info : null;

    public IReadOnlyList<BackgroundTaskInfo> GetAllTasks()
        => _tasks.Values.ToList();

    // ── UI helpers ────────────────────────────────────────────────────────────

    private static Dispatcher? UiDispatcher =>
        Application.Current?.Dispatcher;

    private void UiAdd(BackgroundTaskInfo info) =>
        RunOnUi(() => Tasks.Add(info));

    private void UiRefresh(BackgroundTaskInfo info) => RunOnUi(() =>
    {
        var idx = Tasks.IndexOf(info);
        if (idx < 0) return;
        // Remove + re-insert forces the DataGrid row to rebind every column
        Tasks.RemoveAt(idx);
        Tasks.Insert(idx, info);
    });

    private void RaiseEvent(BackgroundTaskInfo info) => RunOnUi(() =>
    {
        if (info.Status == BgTaskStatus.Completed)
            TaskCompleted?.Invoke(this, info);
        else
            TaskFailed?.Invoke(this, info);
    });

    private static void RunOnUi(Action action)
    {
        var d = UiDispatcher;
        if (d == null) { action(); return; }
        if (d.CheckAccess()) action();
        else d.InvokeAsync(action);
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private async Task PersistAsync(BackgroundTaskInfo info)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MindForgeDbContext>();
            db.BackgroundTaskRecords.Add(new BackgroundTaskRecord
            {
                Id          = Guid.NewGuid(),
                TaskName    = info.Name,
                Status      = info.Status.ToString(),
                Result      = info.Result,
                Error       = info.Error,
                StartedAt   = info.StartedAt,
                CompletedAt = info.CompletedAt
            });
            await db.SaveChangesAsync();
        }
        catch { /* non-critical — in-memory state still valid */ }
    }
}
