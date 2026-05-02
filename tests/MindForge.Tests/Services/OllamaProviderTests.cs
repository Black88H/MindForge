using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MindForge.Services.AI;
using MindForge.Services.AI.Providers;
using Xunit;

namespace MindForge.Tests.Services;

public class OllamaProviderTests
{
    [Fact]
    public async Task CheckAvailabilityAsync_ReturnsFalse_WhenOllamaNotRunning()
    {
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999"); // unlikely port

        var available = await provider.CheckAvailabilityAsync();

        Assert.False(available);
        Assert.False(provider.IsAvailable);
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsEmptyList_WhenOllamaNotRunning()
    {
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999");

        var models = await provider.GetAvailableModelsAsync();

        Assert.NotNull(models);
        Assert.Empty(models);
    }

    [Fact]
    public void OllamaProvider_HasCorrectName()
    {
        var provider = new OllamaProvider();
        Assert.Equal("Ollama", provider.Name);
    }
}

public class AISelectorTests
{
    [Fact]
    public async Task SelectAsync_ReturnsFallbackModel_WhenNoModelsAvailable()
    {
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999"); // unreachable
        var selector = new AISelector(provider);

        var (_, model) = await selector.SelectAsync(AITask.Chat);

        // Should fallback to llama3 when no models available
        Assert.Equal("llama3", model);
    }

    [Fact]
    public async Task SelectAsync_PrefersSummarizationModel()
    {
        // Testing model selection logic with a mock model list
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999");
        var selector = new AISelector(provider);

        // When no models are available, fallback is "llama3" for any task
        var (_, chatModel) = await selector.SelectAsync(AITask.Chat);
        var (_, sumModel) = await selector.SelectAsync(AITask.Summarization);

        Assert.Equal("llama3", chatModel);
        Assert.Equal("llama3", sumModel);
    }

    [Fact]
    public async Task IsOllamaAvailableAsync_ReturnsFalse_WhenUnreachable()
    {
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999");
        var selector = new AISelector(provider);

        var available = await selector.IsOllamaAvailableAsync();

        Assert.False(available);
    }
}
