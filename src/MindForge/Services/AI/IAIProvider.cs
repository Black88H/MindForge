using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MindForge.Services.AI;

public interface IAIProvider
{
    string Name { get; }
    bool IsAvailable { get; }

    Task<bool> CheckAvailabilityAsync(CancellationToken ct = default);
    Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default);
    Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string model, string prompt, CancellationToken ct = default);
}
