using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class FileIngestionServiceTests
{
    [Fact]
    public async Task IngestPdfAsync_FileNotFound_ReturnsFailure()
    {
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase("TestDb_Ingestion_1")
            .Options;
        using var db = new MindForgeDbContext(options);
        var service = new FileIngestionService(db);
        
        var result = await service.IngestPdfAsync(Guid.NewGuid(), Guid.NewGuid(), "does_not_exist.pdf", "Test PDF");
        
        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
}
