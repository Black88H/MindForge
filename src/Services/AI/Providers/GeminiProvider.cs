using System.Net.Http;
using System.Text;
using System.Text.Json;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;
using MindForge.Utils;

namespace MindForge.Services.AI.Providers;

public class GeminiProvider : IAIProvider
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string Model = "gemini-1.5-flash";

    public string Name => "Gemini";
    public bool IsAvailable { get; private set; }

    public async Task<bool> CheckConnectionAsync()
    {
        var key = GetConfig().GeminiApiKey;
        if (string.IsNullOrEmpty(key)) { IsAvailable = false; return false; }
        try
        {
            var resp = await _http.GetAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models?key={key}");
            IsAvailable = resp.IsSuccessStatusCode;
        }
        catch { IsAvailable = false; }
        return IsAvailable;
    }

    public Task<AIResponse> GenerateExplanationAsync(string question, string context = "")
        => SendAsync(PromptTemplates.QAExplanationPrompt(question, context));

    public Task<AIResponse> GenerateContentAsync(string text, string contentType)
        => SendAsync(PromptTemplates.ContentGenerationPrompt(text, contentType));

    private async Task<AIResponse> SendAsync(string prompt)
    {
        var config = GetConfig();
        if (string.IsNullOrEmpty(config.GeminiApiKey))
            return AIResponse.Failure(Name, "Kein Gemini API-Key konfiguriert");

        try
        {
            var url  = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={config.GeminiApiKey}";
            var body = JsonSerializer.Serialize(new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { maxOutputTokens = 1024 },
            });

            var resp = await _http.PostAsync(url,
                new StringContent(body, Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return AIResponse.Failure(Name, $"HTTP {resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text").GetString() ?? string.Empty;

            var meta   = doc.RootElement.GetProperty("usageMetadata");
            var inTok  = meta.GetProperty("promptTokenCount").GetInt32();
            var outTok = meta.GetProperty("candidatesTokenCount").GetInt32();

            return new AIResponse
            {
                IsSuccess    = true,
                Content      = content,
                ProviderName = Name,
                TokensUsed   = inTok + outTok,
                CostUSD      = CostCalculator.Calculate(Name, inTok, outTok),
            };
        }
        catch (Exception ex) { return AIResponse.Failure(Name, ex.Message); }
    }

    private static AIConfig GetConfig() => AIConfig.FromAppSettings(Configuration.Load());
}
