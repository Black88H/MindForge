using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MindForge.Services.AI;

namespace MindForge.Services.AI.Providers;

public class OllamaProvider : IAIProvider
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };
    private string _baseUrl = "http://localhost:11434";
    private bool _available;

    public string Name => "Ollama";
    public bool IsAvailable => _available;

    public void SetBaseUrl(string url) => _baseUrl = url.TrimEnd('/');

    public async Task<bool> CheckAvailabilityAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync($"{_baseUrl}/api/tags", ct);
            _available = response.IsSuccessStatusCode;
        }
        catch
        {
            _available = false;
        }
        return _available;
    }

    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var models = new List<string>();
        try
        {
            var json = await _http.GetStringAsync($"{_baseUrl}/api/tags", ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("models", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrEmpty(name))
                            models.Add(name);
                    }
                }
            }
        }
        catch { /* Ollama not running */ }
        return models;
    }

    public async Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { model, prompt, stream = false });
        using var req = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"{_baseUrl}/api/generate", req, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("response", out var r)
            ? r.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// Streams a plain-prompt completion via /api/generate.
    /// Uses ResponseHeadersRead so chunks arrive in real-time instead of
    /// waiting for the whole response to buffer first.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(string model, string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new { model, prompt, stream = true });
        var req   = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate")
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        // ResponseHeadersRead = start reading the stream immediately,
        // do NOT buffer the whole response in memory first.
        using var response = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(line)) continue;
            string chunk;
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (!doc.RootElement.TryGetProperty("response", out var r)) continue;
                chunk = r.GetString() ?? string.Empty;
            }
            catch (JsonException) { continue; }

            if (!string.IsNullOrEmpty(chunk))
                yield return chunk;
        }
    }

    /// <summary>
    /// Streams a chat-style response via /api/chat using a messages array.
    /// Preferred over StreamAsync for conversational use — the model receives
    /// proper role-tagged turns (system / user) instead of one flat prompt.
    /// </summary>
    /// <summary>
    /// Calls Ollama /api/embed (batch endpoint) and returns the first embedding vector.
    /// Falls back to /api/embeddings (legacy) on failure.
    /// Returns null if neither endpoint responds correctly.
    /// </summary>
    public async Task<float[]?> GetEmbeddingAsync(
        string text,
        string model,
        CancellationToken ct = default)
    {
        var batch = await GetEmbeddingsBatchAsync([text], model, ct);
        return batch.Count > 0 ? batch[0] : null;
    }

    /// <summary>
    /// Batch-embeds multiple texts in ONE API call via /api/embed.
    /// Falls back to sequential /api/embeddings calls if the batch endpoint
    /// is not available. Returns an empty list on total failure.
    /// </summary>
    public async Task<List<float[]>> GetEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        string model,
        CancellationToken ct = default)
    {
        if (texts.Count == 0) return [];

        // Try /api/embed with array input (Ollama ≥ 0.1.34)
        try
        {
            var body    = JsonSerializer.Serialize(new { model, input = texts });
            using var req      = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync($"{_baseUrl}/api/embed", req, ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("embeddings", out var arr))
                {
                    var result = new List<float[]>();
                    foreach (var item in arr.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Array)
                            result.Add(item.EnumerateArray().Select(v => v.GetSingle()).ToArray());
                    }
                    if (result.Count > 0) return result;
                }
            }
        }
        catch { /* fall through */ }

        // Fallback: sequential legacy /api/embeddings (1 call per text)
        var fallback = new List<float[]>();
        foreach (var text in texts)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var body    = JsonSerializer.Serialize(new { model, prompt = text });
                using var req      = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await _http.PostAsync($"{_baseUrl}/api/embeddings", req, ct);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("embedding", out var arr))
                        fallback.Add(arr.EnumerateArray().Select(v => v.GetSingle()).ToArray());
                }
            }
            catch { /* skip this chunk */ }
        }
        return fallback;
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        string model,
        string systemPrompt,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new { role = "system", content = systemPrompt });
        messages.Add(new { role = "user", content = userMessage });

        var body = JsonSerializer.Serialize(new { model, messages, stream = true });
        var req  = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/chat")
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        using var response = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) != null && !ct.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(line)) continue;
            string chunk;
            try
            {
                // /api/chat format: {"message":{"role":"assistant","content":"…"},"done":false}
                using var doc = JsonDocument.Parse(line);
                if (!doc.RootElement.TryGetProperty("message", out var msg)) continue;
                if (!msg.TryGetProperty("content", out var content)) continue;
                chunk = content.GetString() ?? string.Empty;
            }
            catch (JsonException) { continue; }

            if (!string.IsNullOrEmpty(chunk))
                yield return chunk;
        }
    }
}
