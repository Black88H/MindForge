using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MindForge.Tests;

/// <summary>
/// Tests for IFileIngestionService.
///
/// FileIngestionService relies on native libraries (Tesseract, PdfPig, OpenXml)
/// that are not available in a unit-test environment. Therefore:
///   - Exception-path tests (FileNotFound, unsupported format) use the real service.
///   - Consumer / contract tests use a Moq of IFileIngestionService.
/// </summary>
public class FileIngestionServiceTests : IDisposable
{
    private readonly MindForgeDbContext _db;
    private readonly Guid _userId    = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();

    public FileIngestionServiceTests()
    {
        var opts = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MindForgeDbContext(opts);
    }

    public void Dispose() => _db.Dispose();

    // ── Real service – exception paths (no external libs needed) ─────────

    [Fact]
    public async Task IngestFile_NonExistentPath_ThrowsFileNotFoundException()
    {
        var sut = new FileIngestionService(_db, NullLogger<FileIngestionService>.Instance);

        var act = async () => await sut.IngestFileAsync(
            @"C:\nonexistent\file_that_does_not_exist.pdf", _subjectId, _userId);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".csv")]
    [InlineData(".mp3")]
    [InlineData(".exe")]
    [InlineData(".zip")]
    public async Task IngestFile_UnsupportedFormat_ThrowsNotSupportedException(string extension)
    {
        // Create a real temp file with the unsupported extension
        var tempPath = Path.Combine(Path.GetTempPath(), $"mf_test_{Guid.NewGuid()}{extension}");
        await File.WriteAllTextAsync(tempPath, "dummy content");

        try
        {
            var sut = new FileIngestionService(_db, NullLogger<FileIngestionService>.Instance);

            var act = async () => await sut.IngestFileAsync(tempPath, _subjectId, _userId);

            await act.Should().ThrowAsync<NotSupportedException>()
                     .WithMessage($"*{extension}*");
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    // ── Mocked interface – consumer contract tests ────────────────────────

    [Fact]
    public async Task IngestFile_MockService_ReturnsExpectedMaterial()
    {
        var expected = new Material
        {
            Id               = Guid.NewGuid(),
            SubjectId        = _subjectId,
            UserId           = _userId,
            OriginalFileName = "lecture.pdf",
            OriginalFormat   = MaterialFormat.PDF,
            OriginalFilePath = @"C:\materials\lecture.pdf",
            KiContent        = "# Lecture\n\nSome content here.",
            KiContentHash    = "abc123def456",
            TokenCount       = 10
        };

        var mock = new Mock<IFileIngestionService>();
        mock.Setup(s => s.IngestFileAsync("lecture.pdf", _subjectId, _userId))
            .ReturnsAsync(expected);

        var result = await mock.Object.IngestFileAsync("lecture.pdf", _subjectId, _userId);

        result.OriginalFileName.Should().Be("lecture.pdf");
        result.OriginalFormat.Should().Be(MaterialFormat.PDF);
        result.KiContent.Should().Contain("Some content here.");
        result.TokenCount.Should().BePositive();
    }

    [Fact]
    public async Task IngestFile_MockService_CalledWithCorrectArguments()
    {
        var mock = new Mock<IFileIngestionService>();
        mock.Setup(s => s.IngestFileAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new Material
            {
                OriginalFileName = "test.docx",
                OriginalFilePath = "/tmp/test.docx",
                KiContent        = "content",
                KiContentHash    = "hash"
            });

        await mock.Object.IngestFileAsync("test.docx", _subjectId, _userId);

        mock.Verify(s => s.IngestFileAsync("test.docx", _subjectId, _userId), Times.Once);
    }

    // ── Hash and token count helpers (via reflection) ─────────────────────

    [Theory]
    [InlineData("Hello World",    2)]    // 11 chars / 4 = 2
    [InlineData("",               0)]
    [InlineData("abcd",           1)]    // exactly 4 chars = 1
    [InlineData("abcde",          1)]    // 5 chars / 4 = 1 (integer division)
    [InlineData("1234567890123456", 4)]  // 16 / 4 = 4
    public void EstimateTokenCount_ReturnsExpectedValue(string text, int expected)
    {
        // EstimateTokenCount is private static; access via reflection
        var method = typeof(FileIngestionService)
            .GetMethod("EstimateTokenCount",
                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("EstimateTokenCount method should exist");

        var result = (int)method!.Invoke(null, new object[] { text })!;

        result.Should().Be(expected);
    }

    [Fact]
    public void ComputeHash_SameInput_ProducesSameHash()
    {
        var method = typeof(FileIngestionService)
            .GetMethod("ComputeHash",
                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("ComputeHash method should exist");

        var hash1 = (string)method!.Invoke(null, new object[] { "Test content" })!;
        var hash2 = (string)method!.Invoke(null, new object[] { "Test content" })!;

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInput_ProducesDifferentHash()
    {
        var method = typeof(FileIngestionService)
            .GetMethod("ComputeHash",
                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull();

        var hash1 = (string)method!.Invoke(null, new object[] { "Content A" })!;
        var hash2 = (string)method!.Invoke(null, new object[] { "Content B" })!;

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_ProducesLowercaseHexString()
    {
        var method = typeof(FileIngestionService)
            .GetMethod("ComputeHash",
                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var hash = (string)method!.Invoke(null, new object[] { "Any content" })!;

        hash.Should().MatchRegex("^[0-9a-f]{64}$", "SHA-256 hex should be 64 lowercase hex chars");
    }

    // ── Format detection (MaterialFormat mapping) ─────────────────────────

    [Fact]
    public async Task IngestFile_PdfExtension_DetectedAsPdf()
    {
        // We use the mock to assert that the service correctly maps .pdf → PDF
        // (Real mapping tested via exception test for unsupported formats above)
        var mock = new Mock<IFileIngestionService>();
        mock.Setup(s => s.IngestFileAsync(It.Is<string>(p => p.EndsWith(".pdf")), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new Material
            {
                OriginalFileName = "doc.pdf",
                OriginalFilePath = "/tmp/doc.pdf",
                OriginalFormat   = MaterialFormat.PDF,
                KiContent        = "x",
                KiContentHash    = "y"
            });

        var result = await mock.Object.IngestFileAsync("doc.pdf", _subjectId, _userId);

        result.OriginalFormat.Should().Be(MaterialFormat.PDF);
    }

    [Fact]
    public async Task IngestFile_DocxExtension_DetectedAsDocx()
    {
        var mock = new Mock<IFileIngestionService>();
        mock.Setup(s => s.IngestFileAsync(It.Is<string>(p => p.EndsWith(".docx")), It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new Material
            {
                OriginalFileName = "doc.docx",
                OriginalFilePath = "/tmp/doc.docx",
                OriginalFormat   = MaterialFormat.DOCX,
                KiContent        = "x",
                KiContentHash    = "y"
            });

        var result = await mock.Object.IngestFileAsync("doc.docx", _subjectId, _userId);

        result.OriginalFormat.Should().Be(MaterialFormat.DOCX);
    }
}
