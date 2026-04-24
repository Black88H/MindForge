using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Providers;
using MindForge.Utils;

namespace MindForge.Services.AI.Selection;

public class AISelector : IAISelector
{
    private readonly InternetDetector _internet;
    private readonly TaskAnalyzer _taskAnalyzer;
    private readonly ITokenTracker _tokenTracker;
    private readonly Dictionary<string, IAIProvider> _providers;

    public AISelector(
        InternetDetector internet,
        TaskAnalyzer taskAnalyzer,
        ITokenTracker tokenTracker,
        ClaudeAIProvider claude,
        OpenAIProvider openAI,
        GeminiProvider gemini,
        OllamaProvider ollama)
    {
        _internet = internet;
        _taskAnalyzer = taskAnalyzer;
        _tokenTracker = tokenTracker;
        _providers = new Dictionary<string, IAIProvider>(StringComparer.OrdinalIgnoreCase)
        {
            ["Claude"] = claude,
            ["OpenAI"] = openAI,
            ["Gemini"] = gemini,
            ["Ollama"] = ollama,
        };
    }

    public async Task<IAIProvider> SelectProviderAsync(TaskType task)
    {
        var config   = AIConfig.FromAppSettings(Configuration.Load());
        var isOnline = await _internet.IsOnlineAsync();

        if (!config.AutoSelectProvider
            && _providers.TryGetValue(config.DefaultProvider, out var preferred)
            && await preferred.CheckConnectionAsync())
            return preferred;

        foreach (var name in _taskAnalyzer.GetPreferredProviders(task))
        {
            if (!isOnline && !string.Equals(name, "Ollama", StringComparison.OrdinalIgnoreCase))
                continue;

            if (_providers.TryGetValue(name, out var provider)
                && await provider.CheckConnectionAsync())
                return provider;
        }

        return _providers["Ollama"];
    }

    public async Task<AIResponse> ExecuteAsync(TaskType task, string prompt, string context = "")
    {
        var provider = await SelectProviderAsync(task);

        var response = task == TaskType.ContentGeneration
            ? await provider.GenerateContentAsync(prompt, context)
            : await provider.GenerateExplanationAsync(prompt, context);

        if (response.IsSuccess)
            await _tokenTracker.TrackUsageAsync(provider.Name, task.ToString(), response.TokensUsed, response.CostUSD);

        return response;
    }

    public string GetSelectedProviderName(TaskType task)
    {
        var config = Configuration.Load();
        return config.AutoSelectProvider
            ? _taskAnalyzer.GetPreferredProviders(task)[0]
            : config.DefaultProvider;
    }
}
