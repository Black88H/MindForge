using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;
using MindForge.Utils;

namespace MindForge.Services.AI.Providers;

public class OpenAIProvider : IAIProvider
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string Model   = "gpt-4o-mini";

    public string Name => "OpenAI";
    public bool IsAvailable { get; private set; }

    public async Task<bool> CheckConnectionAsync()
    {
        var key = GetConfig().OpenAIApiKey;
        if (string.IsNullOrEmpty(key)) { IsAvailable = false; return false; }
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            IsAvailable = (await _http.SendAsync(req)).IsSuccessStatusCode;
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
        if (string.IsNullOrEmpty(config.OpenAIApiKey))
            return AIResponse.Failure(Name, "Kein OpenAI API-Key konfiguriert");

        try
        {
            var body = JsonSerializer.Serialize(new
            {
                model = Model,
                max_tokens = 1024,
                messages = new[] { new { role = "user", content = prompt } },
            });

            var req = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.OpenAIApiKey);

            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return AIResponse.Failure(Name, $"HTTP {resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content").GetString() ?? string.Empty;

            var usage  = doc.RootElement.GetProperty("usage");
            var inTok  = usage.GetProperty("prompt_tokens").GetInt32();
            var outTok = usage.GetProperty("completion_tokens").GetInt32();

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
