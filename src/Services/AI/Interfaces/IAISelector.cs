using MindForge.Services.AI.Models;

namespace MindForge.Services.AI.Interfaces;

public interface IAISelector
{
    Task<IAIProvider> SelectProviderAsync(TaskType task);
    Task<AIResponse> ExecuteAsync(TaskType task, string prompt, string context = "");
    string GetSelectedProviderName(TaskType task);
}
