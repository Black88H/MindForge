using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class BackgroundTasksWindow : Window
{
    private readonly IBackgroundTaskService _bgService;

    public BackgroundTasksWindow(IBackgroundTaskService bgService)
    {
        InitializeComponent();
        _bgService = bgService;

        // Bind the live ObservableCollection directly to the ItemsControl
        TasksList.ItemsSource = _bgService.Tasks;

        // Keep header count + empty-state TextBlock in sync
        _bgService.Tasks.CollectionChanged += (_, _) => RefreshHeader();
        RefreshHeader();
    }

    // ── Header helpers ────────────────────────────────────────────────────────

    private void RefreshHeader()
    {
        var running = _bgService.Tasks.Count(t => t.IsRunning);
        TxtRunningCount.Text = $"{running} aktiv";
        TxtEmpty.Visibility  = _bgService.Tasks.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void OnCancelTask(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string taskId)
            _bgService.CancelTask(taskId);
    }

    private void OnCancelAll(object sender, RoutedEventArgs e)
    {
        foreach (var t in _bgService.Tasks.Where(t => t.IsRunning).ToList())
            _bgService.CancelTask(t.TaskId);
    }
}
