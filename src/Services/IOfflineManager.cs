namespace MindForge.Services;

public interface IOfflineManager
{
    bool IsOfflineMode { get; }
    bool IsOnline { get; }
    Task SyncAsync();
    Task EnableOfflineModeAsync();
    Task DisableOfflineModeAsync();
    event EventHandler<bool> ConnectivityChanged;
}
