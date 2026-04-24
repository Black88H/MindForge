namespace MindForge.Services.AI.Models;

public class AIResponse
{
    public bool IsSuccess { get; init; }
    public string Content { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public int TokensUsed { get; init; }
    public double CostUSD { get; init; }
    public string? Error { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static AIResponse Failure(string provider, string error) => new()
    {
        IsSuccess = false,
        ProviderName = provider,
        Error = error,
    };
}
