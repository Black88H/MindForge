using MindForge.Models;

namespace MindForge.Services.AI.Models;

public class AIConfig
{
    public string ClaudeApiKey { get; set; } = string.Empty;
    public string OpenAIApiKey { get; set; } = string.Empty;
    public string GeminiApiKey { get; set; } = string.Empty;
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public bool AutoSelectProvider { get; set; } = true;
    public string DefaultProvider { get; set; } = "Claude";
    public decimal TokenBudgetUSD { get; set; } = 5.0m;
    public int TimeoutSeconds { get; set; } = 30;

    public static AIConfig FromAppSettings(AppSettings s) => new()
    {
        ClaudeApiKey       = s.ClaudeApiKey,
        OpenAIApiKey       = s.OpenAiApiKey,
        GeminiApiKey       = s.GeminiApiKey,
        OllamaEndpoint     = s.OllamaEndpoint,
        AutoSelectProvider = s.AutoSelectProvider,
        DefaultProvider    = s.DefaultProvider,
        TokenBudgetUSD     = s.TokenBudgetUSD,
    };
}
