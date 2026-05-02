using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MindForge.Services.AI.Providers;

namespace MindForge.Services.AI;

public enum AITask { Chat, Summarization, QnA, StudyGuide, CodeMath }

public class AISelector
{
    private readonly OllamaProvider _ollama;
    private List<string> _cachedModels = new();
    private DateTime _modelsCachedAt = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public AISelector(OllamaProvider ollama) => _ollama = ollama;

    public void SetOllamaUrl(string url) => _ollama.SetBaseUrl(url);

    public async Task<(IAIProvider provider, string model)> SelectAsync(AITask task, CancellationToken ct = default)
    {
        await EnsureModelsRefreshedAsync(ct);

        var model = PickModel(task, _cachedModels);
        return (_ollama, model);
    }

    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        await EnsureModelsRefreshedAsync(ct);
        return _cachedModels;
    }

    public async Task<bool> IsOllamaAvailableAsync(CancellationToken ct = default)
        => await _ollama.CheckAvailabilityAsync(ct);

    private async Task EnsureModelsRefreshedAsync(CancellationToken ct)
    {
        if (DateTime.UtcNow - _modelsCachedAt < CacheTtl) return;
        _cachedModels = await _ollama.GetAvailableModelsAsync(ct);
        _modelsCachedAt = DateTime.UtcNow;
    }

    private static string PickModel(AITask task, List<string> models)
    {
        if (models.Count == 0) return "llama3";

        // Prefer models by task using substring matching on known model families
        var candidates = task switch
        {
            AITask.Summarization => PreferModels(models,
                ["llama3.1", "llama3.2", "mistral", "phi3", "gemma2", "llama3", "qwen"]),
            AITask.QnA => PreferModels(models,
                ["llama3", "mistral", "phi3", "gemma", "qwen", "solar"]),
            AITask.StudyGuide => PreferModels(models,
                ["llama3.1", "llama3", "mistral", "phi3", "gemma"]),
            AITask.CodeMath => PreferModels(models,
                ["codellama", "deepseek-coder", "qwen2.5-coder", "phi3", "llama3", "mistral"]),
            _ => models
        };

        return candidates.FirstOrDefault() ?? models.First();
    }

    private static IEnumerable<string> PreferModels(List<string> available, string[] preference)
    {
        foreach (var pref in preference)
        {
            var match = available.FirstOrDefault(m =>
                m.Contains(pref, StringComparison.OrdinalIgnoreCase));
            if (match != null) return [match];
        }
        return available.Take(1);
    }
}
