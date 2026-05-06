using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public interface IChatService
{
    Task<string> SendMessageAsync(Guid userId, string prompt, string context = "", CancellationToken ct = default);
    IAsyncEnumerable<string> StreamMessageAsync(
        Guid userId, string prompt,
        string context       = "",
        string systemPrompt  = "",
        string? modelOverride = null,
        CancellationToken ct = default);
    Task<List<ChatMessage>> GetHistoryAsync(Guid userId, int limit = 50);
    Task ClearHistoryAsync(Guid userId);
}
