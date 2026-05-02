using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.AI;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class ChatService : IChatService
{
    private readonly MindForgeDbContext _db;
    private readonly AISelector _ai;
    private const string FallbackResponse = "The AI tutor is currently unavailable. Please ensure Ollama is running.";

    public ChatService(MindForgeDbContext db, AISelector ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<string> SendMessageAsync(Guid userId, string prompt, string context = "",
        CancellationToken ct = default)
    {
        await SaveMessageAsync(userId, prompt, ChatRole.User);

        var responseText = FallbackResponse;
        try
        {
            var fullPrompt = BuildPrompt(prompt, context);
            var (provider, model) = await _ai.SelectAsync(AITask.Chat, ct);
            responseText = await provider.GenerateAsync(model, fullPrompt, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Ollama unreachable */ }

        await SaveMessageAsync(userId, responseText, ChatRole.Assistant);
        return responseText;
    }

    public async IAsyncEnumerable<string> StreamMessageAsync(Guid userId, string prompt,
        string context = "", [EnumeratorCancellation] CancellationToken ct = default)
    {
        await SaveMessageAsync(userId, prompt, ChatRole.User);

        var fullPrompt = BuildPrompt(prompt, context);
        var chunks = await CollectStreamAsync(fullPrompt, ct);

        var fullResponse = new System.Text.StringBuilder();
        foreach (var chunk in chunks)
        {
            fullResponse.Append(chunk);
            yield return chunk;
        }

        await SaveMessageAsync(userId, fullResponse.ToString(), ChatRole.Assistant);
    }

    private async Task<System.Collections.Generic.List<string>> CollectStreamAsync(
        string prompt, CancellationToken ct)
    {
        var result = new System.Collections.Generic.List<string>();
        try
        {
            var (provider, model) = await _ai.SelectAsync(AITask.Chat, ct);
            await foreach (var chunk in provider.StreamAsync(model, prompt, ct))
                result.Add(chunk);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Ollama unreachable */ }

        if (result.Count == 0)
            result.Add(FallbackResponse);

        return result;
    }

    public async Task<List<ChatMessage>> GetHistoryAsync(Guid userId, int limit = 50)
    {
        return await _db.ChatMessages
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task ClearHistoryAsync(Guid userId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.UserId == userId)
            .ToListAsync();
        _db.ChatMessages.RemoveRange(messages);
        await _db.SaveChangesAsync();
    }

    private async Task SaveMessageAsync(Guid userId, string content, ChatRole role)
    {
        _db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Content = content,
            Role = role,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private static string BuildPrompt(string userMessage, string context)
    {
        if (string.IsNullOrWhiteSpace(context))
            return userMessage;

        return $"""
            You are a helpful AI tutor. Answer based on the provided context when available.

            Context from study materials:
            {context}

            User question: {userMessage}
            """;
    }
}
