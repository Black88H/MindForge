using MindForge.Services.AI.Models;

namespace MindForge.Services;

public interface IAIProvider
{
    string Name { get; }
    bool IsAvailable { get; }
    Task<AIResponse> GenerateExplanationAsync(string question, string context = "");
    Task<AIResponse> GenerateContentAsync(string text, string contentType);
    Task<bool> CheckConnectionAsync();
}
