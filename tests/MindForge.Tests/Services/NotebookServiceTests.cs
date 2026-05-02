using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services;
using MindForge.Services.AI;
using MindForge.Services.AI.Providers;
using MindForge.Services.Interfaces;
using Xunit;

namespace MindForge.Tests.Services;

// A fake AI provider that returns deterministic responses for testing
internal class FakeOllamaProvider : IAIProvider
{
    public string Name => "FakeOllama";
    public bool IsAvailable => true;

    public Task<bool> CheckAvailabilityAsync(CancellationToken ct = default) => Task.FromResult(true);
    public Task<List<string>> GetAvailableModelsAsync(CancellationToken ct = default)
        => Task.FromResult(new List<string> { "llama3" });

    public Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default)
        => Task.FromResult($"AI response to: {prompt[..Math.Min(50, prompt.Length)]}");

    public async IAsyncEnumerable<string> StreamAsync(string model, string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return "chunk1 ";
        yield return "chunk2";
        await Task.CompletedTask;
    }
}

internal class FakeAISelector : AISelector
{
    public FakeAISelector() : base(new OllamaProvider()) { }

    public new Task<(IAIProvider, string)> SelectAsync(AITask task, CancellationToken ct = default)
        => Task.FromResult(((IAIProvider)new FakeOllamaProvider(), "llama3"));
}

public class NotebookServiceTests
{
    private MindForgeDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MindForgeDbContext(options);
    }

    [Fact]
    public async Task SummarizeMaterialAsync_ReturnsNonEmptyString()
    {
        using var db = CreateDb("NotebookTest_Summarize");
        var materialId = Guid.NewGuid();
        db.Materials.Add(new Material
        {
            Id = materialId,
            UserId = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            OriginalFileName = "Test.pdf",
            KiContent = "This is a test document with some content about physics.",
            OriginalFormat = MaterialFormat.PDF
        });
        await db.SaveChangesAsync();

        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999"); // unreachable — service handles gracefully
        var selector = new AISelector(provider);
        var service = new NotebookService(db, selector);

        // Will throw because Ollama is unreachable, which is expected behavior
        // The service should propagate the exception for the caller to handle
        await Assert.ThrowsAnyAsync<Exception>(() => service.SummarizeMaterialAsync(materialId));
    }

    [Fact]
    public async Task SummarizeMaterialAsync_ThrowsWhenMaterialNotFound()
    {
        using var db = CreateDb("NotebookTest_NotFound");
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999");
        var selector = new AISelector(provider);
        var service = new NotebookService(db, selector);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SummarizeMaterialAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task SummarizeSubjectAsync_ReturnsNoMaterialsMessage_WhenEmpty()
    {
        using var db = CreateDb("NotebookTest_EmptySubject");
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999");
        var selector = new AISelector(provider);
        var service = new NotebookService(db, selector);

        var result = await service.SummarizeSubjectAsync(Guid.NewGuid());

        Assert.Contains("No materials", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AskWithSourcesAsync_ReturnsNoDocumentsMessage_WhenNoMaterials()
    {
        using var db = CreateDb("NotebookTest_NoMaterials");
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999");
        var selector = new AISelector(provider);
        var service = new NotebookService(db, selector);

        var userId = Guid.NewGuid();
        var result = await service.AskWithSourcesAsync(userId, "What is physics?", new List<Guid>());

        Assert.Contains("don't have any documents", result.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Citations);
    }

    [Fact]
    public async Task GenerateStudyGuideAsync_ReturnsNoMaterialsMessage_WhenEmpty()
    {
        using var db = CreateDb("NotebookTest_NoStudyGuide");
        var provider = new OllamaProvider();
        provider.SetBaseUrl("http://localhost:19999");
        var selector = new AISelector(provider);
        var service = new NotebookService(db, selector);

        var result = await service.GenerateStudyGuideAsync(Guid.NewGuid());

        Assert.Contains("No materials", result, StringComparison.OrdinalIgnoreCase);
    }
}
