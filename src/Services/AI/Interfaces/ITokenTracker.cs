namespace MindForge.Services.AI.Interfaces;

public interface ITokenTracker
{
    Task TrackUsageAsync(string provider, string taskType, int tokens, double cost);
    Task<int> GetTotalTokensAsync(int days = 30);
    Task<double> GetTotalCostAsync(int days = 30);
}
