using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindForge.Models;
using MindForge.Services.AI.Interfaces;

namespace MindForge.Services;

public interface IChatService
{
    Task<ChatMessage> SendMessageAsync(Guid userId, Guid? subjectId, string message, string? imagePath = null);
    Task<List<ChatMessage>> GetChatHistoryAsync(Guid userId, Guid? subjectId, int skip = 0, int take = 50);
    Task ClearChatAsync(Guid userId, Guid? subjectId);
}

public class ChatService : IChatService
{
    private readonly MindForgeDbContext _db;
    private readonly IAISelector _ai;
    private readonly ILogger<ChatService> _logger;
    private const int MaxHistoryMessages = 20;

    public ChatService(MindForgeDbContext db, IAISelector ai, ILogger<ChatService> logger)
    {
        _db = db;
        _ai = ai;
        _logger = logger;
    }

    public async Task<ChatMessage> SendMessageAsync(Guid userId, Guid? subjectId, string message, string? imagePath = null)
    {
        // Kontext sammeln
        var contextBuilder = new StringBuilder();

        if (subjectId.HasValue)
        {
            var subject = await _db.Set<Subject>().FindAsync(subjectId.Value);
            contextBuilder.AppendLine($"Fach: {subject?.Name ?? "Unbekannt"}");

            // Materialien des Fachs als Kontext laden
            var materials = await _db.Materials
                .Where(m => m.SubjectId == subjectId.Value && m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(5) // Max 5 neueste Materialien
                .ToListAsync();

            if (materials.Any())
            {
                contextBuilder.AppendLine("\n--- Lernmaterial-Kontext ---");
                foreach (var mat in materials)
                {
                    // Token-Limit: max 2000 Zeichen pro Material
                    var content = mat.KiContent.Length > 2000
                        ? mat.KiContent[..2000] + "\n[... gekürzt]"
                        : mat.KiContent;
                    contextBuilder.AppendLine(content);
                    contextBuilder.AppendLine();
                }
            }
        }

        // Chat-History laden
        var history = await _db.ChatMessages
            .Where(m => m.UserId == userId && m.SubjectId == subjectId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxHistoryMessages)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var historyText = new StringBuilder();
        foreach (var msg in history)
        {
            var role = msg.Role == ChatRole.User ? "Nutzer" : "KI-Tutor";
            historyText.AppendLine($"{role}: {msg.Content}");
        }

        var systemPrompt = $@"Du bist ein KI-Tutor in der Lernapp MindForge. 
Du hilfst Schülern und Studierenden beim Lernen.
Erkläre Sachverhalte klar, strukturiert und auf dem Niveau des Nutzers.
Nutze Beispiele und Analogien. Antworte auf Deutsch.

{contextBuilder}

Bisheriger Chatverlauf:
{historyText}";

        var fullPrompt = $"{systemPrompt}\n\nNutzer: {message}";

        // User-Nachricht speichern
        var userMsg = new ChatMessage
        {
            UserId = userId,
            SubjectId = subjectId,
            Role = ChatRole.User,
            Content = message,
            ImagePath = imagePath
        };
        _db.ChatMessages.Add(userMsg);

        // KI-Antwort generieren
        var response = await _ai.ExecuteAsync(AI.Models.TaskType.QAExplanation, fullPrompt);

        var aiMsg = new ChatMessage
        {
            UserId = userId,
            SubjectId = subjectId,
            Role = ChatRole.Assistant,
            Content = response.Content,
            TokensUsed = response.TokensUsed,
            Provider = response.ProviderName ?? "unknown"
        };
        _db.ChatMessages.Add(aiMsg);

        await _db.SaveChangesAsync();
        return aiMsg;
    }

    public async Task<List<ChatMessage>> GetChatHistoryAsync(Guid userId, Guid? subjectId, int skip = 0, int take = 50)
    {
        return await _db.ChatMessages
            .Where(m => m.UserId == userId && m.SubjectId == subjectId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(skip)
            .Take(take)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task ClearChatAsync(Guid userId, Guid? subjectId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.UserId == userId && m.SubjectId == subjectId)
            .ToListAsync();
        _db.ChatMessages.RemoveRange(messages);
        await _db.SaveChangesAsync();
    }
}
