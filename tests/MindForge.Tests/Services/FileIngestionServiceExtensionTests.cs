using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class FileIngestionServiceExtensionTests
{
    private MindForgeDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new MindForgeDbContext(options);
    }

    [Fact]
    public async Task IngestTextAsync_Succeeds_WithValidTextFile()
    {
        using var db = CreateDb("FileIngestion_Text");
        var service = new FileIngestionService(db);

        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, "Hello world. This is test content.");

        try
        {
            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var result = await service.IngestTextAsync(userId, subjectId, tmpFile, "Test Text");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal("Test Text", result.Data!.OriginalFileName);
            Assert.Contains("Hello world", result.Data.KiContent);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task IngestTextAsync_Fails_WhenFileNotFound()
    {
        using var db = CreateDb("FileIngestion_Text_NotFound");
        var service = new FileIngestionService(db);

        var result = await service.IngestTextAsync(
            Guid.NewGuid(), Guid.NewGuid(), "/nonexistent/path/file.txt", "Missing");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task IngestPdfAsync_Fails_WhenFileNotFound()
    {
        using var db = CreateDb("FileIngestion_PDF_NotFound");
        var service = new FileIngestionService(db);

        var result = await service.IngestPdfAsync(
            Guid.NewGuid(), Guid.NewGuid(), "/nonexistent/file.pdf", "Missing");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task IngestWebUrlAsync_Fails_WithInvalidUrl()
    {
        using var db = CreateDb("FileIngestion_Web_Invalid");
        var service = new FileIngestionService(db);

        var result = await service.IngestWebUrlAsync(
            Guid.NewGuid(), Guid.NewGuid(), "http://localhost:19999/nonexistent", "Test");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task IngestTextAsync_SavesMaterialToDatabase()
    {
        using var db = CreateDb("FileIngestion_Text_DB");
        var service = new FileIngestionService(db);

        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, "Database persistence test content.");

        try
        {
            var userId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var result = await service.IngestTextAsync(userId, subjectId, tmpFile, "DB Test");

            Assert.True(result.IsSuccess);
            var saved = await db.Materials.FindAsync(result.Data!.Id);
            Assert.NotNull(saved);
            Assert.Equal("DB Test", saved!.OriginalFileName);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
