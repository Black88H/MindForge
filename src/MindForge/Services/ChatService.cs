using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class ChatService : IChatService
{
    private readonly MindForgeDbContext _db;
    private static readonly HttpClient _httpClient = new HttpClient();
    private const string OllamaUrl = "http://localhost:11434/api/generate";

    public ChatService(MindForgeDbContext db)
    {
        _db = db;
    }

    public async Task<string> SendMessageAsync(Guid userId, string prompt, string context = "")
    {
        // 1. Speichere User Nachricht
        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Content = prompt,
            Role = ChatRole.User,
            CreatedAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(userMessage);
        await _db.SaveChangesAsync(); // Damit der User direkt sein Feedback hat

        // 2. Rufe Ollama API auf
        string responseText = "Entschuldigung, der KI-Tutor ist derzeit nicht erreichbar.";
        
        try
        {
            var requestBody = new
            {
                model = "llama3",
                prompt = string.IsNullOrEmpty(context) ? prompt : $"Context: {context}\n\nUser: {prompt}",
                stream = false
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(OllamaUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                if (result.TryGetProperty("response", out var resp))
                {
                    responseText = resp.GetString() ?? responseText;
                }
            }
        }
        catch (Exception)
        {
            // Logging würde hier stattfinden
        }

        // 3. Speichere Bot Antwort
        var botMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Content = responseText,
            Role = ChatRole.Assistant,
            CreatedAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(botMessage);
        
        await _db.SaveChangesAsync();

        return responseText;
    }
}
