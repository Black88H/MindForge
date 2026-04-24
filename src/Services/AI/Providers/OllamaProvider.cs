using System.Net.Http;
using System.Text;
using System.Text.Json;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Selection;
using MindForge.Services.AI.Utilities;
using MindForge.Utils;

namespace MindForge.Services.AI.Providers;

public class OllamaProvider : IAIProvider
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private readonly HardwareDetector _hardware;

    public string Name => "Ollama";
    public bool IsAvailable { get; private set; }

    public OllamaProvider(HardwareDetector hardware) => _hardware = hardware;

    public async Task<bool> CheckConnectionAsync()
    {
        var endpoint = GetConfig().OllamaEndpoint;
        try
        {
            var resp = await _http.GetAsync($"{endpoint}/api/tags");
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
        var model  = _hardware.GetRecommendedOfflineModel();

        try
        {
            var url  = $"{config.OllamaEndpoint}/api/generate";
            var body = JsonSerializer.Serialize(new { model, prompt, stream = false });

            var resp = await _http.PostAsync(url,
                new StringContent(body, Encoding.UTF8, "application/json"));
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return AIResponse.Failure(Name, $"Ollama HTTP {resp.StatusCode}: {json}");

            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
            var tokens  = doc.RootElement.TryGetProperty("eval_count", out var ec) ? ec.GetInt32() : 0;

            return new AIResponse
            {
                IsSuccess    = true,
                Content      = content,
                ProviderName = $"{Name} ({model})",
                TokensUsed   = tokens,
                CostUSD      = 0,
            };
        }
        catch (Exception ex) { return AIResponse.Failure(Name, ex.Message); }
    }

    private static AIConfig GetConfig() => AIConfig.FromAppSettings(Configuration.Load());
}
