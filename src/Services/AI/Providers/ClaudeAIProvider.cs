using System.Net.Http;
using System.Text;
using System.Text.Json;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;
using MindForge.Utils;

namespace MindForge.Services.AI.Providers;

public class ClaudeAIProvider : IAIProvider
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model   = "claude-3-5-haiku-20241022";

    public string Name => "Claude";
    public bool IsAvailable { get; private set; }

    public async Task<bool> CheckConnectionAsync()
    {
        var key = GetConfig().ClaudeApiKey;
        if (string.IsNullOrEmpty(key)) { IsAvailable = false; return false; }
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models");
            req.Headers.Add("x-api-key", key);
            req.Headers.Add("anthropic-version", "2023-06-01");
            var resp = await _http.SendAsync(req);
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
        if (string.IsNullOrEmpty(config.ClaudeApiKey))
            return AIResponse.Failure(Name, "Kein Claude API-Key konfiguriert");

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
            req.Headers.Add("x-api-key", config.ClaudeApiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");

            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return AIResponse.Failure(Name, $"HTTP {resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text").GetString() ?? string.Empty;

            var usage = doc.RootElement.GetProperty("usage");
            var inTok  = usage.GetProperty("input_tokens").GetInt32();
            var outTok = usage.GetProperty("output_tokens").GetInt32();

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
