using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class FileIngestionService : IFileIngestionService
{
    private readonly MindForgeDbContext _db;
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public FileIngestionService(MindForgeDbContext db) => _db = db;

    public async Task<Result<Material>> IngestPdfAsync(Guid userId, Guid subjectId, string filePath, string title)
    {
        if (!File.Exists(filePath))
            return Result<Material>.Failure("File not found.");

        try
        {
            var text = await ExtractTextFromPdfAsync(filePath);
            return await SaveMaterialAsync(userId, subjectId, title, filePath, MaterialFormat.PDF, text);
        }
        catch (Exception ex)
        {
            return Result<Material>.Failure($"Error reading PDF: {ex.Message}");
        }
    }

    public async Task<Result<Material>> IngestDocxAsync(Guid userId, Guid subjectId, string filePath, string title)
    {
        if (!File.Exists(filePath))
            return Result<Material>.Failure("File not found.");

        try
        {
            var text = await Task.Run(() => ExtractTextFromDocx(filePath));
            return await SaveMaterialAsync(userId, subjectId, title, filePath, MaterialFormat.DOCX, text);
        }
        catch (Exception ex)
        {
            return Result<Material>.Failure($"Error reading DOCX: {ex.Message}");
        }
    }

    public async Task<Result<Material>> IngestTextAsync(Guid userId, Guid subjectId, string filePath, string title)
    {
        if (!File.Exists(filePath))
            return Result<Material>.Failure("File not found.");

        try
        {
            var text = await File.ReadAllTextAsync(filePath);
            return await SaveMaterialAsync(userId, subjectId, title, filePath, MaterialFormat.PDF, text);
        }
        catch (Exception ex)
        {
            return Result<Material>.Failure($"Error reading file: {ex.Message}");
        }
    }

    public async Task<Result<Material>> IngestWebUrlAsync(Guid userId, Guid subjectId, string url, string title,
        CancellationToken ct = default)
    {
        try
        {
            _http.DefaultRequestHeaders.UserAgent.TryParseAdd("MindForge/3.0 (+https://github.com/Black88H/MindForge)");
            var html = await _http.GetStringAsync(url, ct);
            var text = StripHtml(html);
            return await SaveMaterialAsync(userId, subjectId, title, url, MaterialFormat.PDF, text);
        }
        catch (Exception ex)
        {
            return Result<Material>.Failure($"Error fetching URL: {ex.Message}");
        }
    }

    public Task<string> ExtractTextFromPdfAsync(string filePath)
    {
        return Task.Run(() =>
        {
            var sb = new StringBuilder();
            using var document = PdfDocument.Open(filePath);
            foreach (var page in document.GetPages())
            {
                var text = page.Text;
                if (string.IsNullOrWhiteSpace(text))
                    text = TryOcrPage(page);
                sb.AppendLine(text);
            }
            return sb.ToString();
        });
    }

    private static string ExtractTextFromDocx(string filePath)
    {
        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        foreach (var para in body.Descendants<Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }
        return sb.ToString();
    }

    private static string StripHtml(string html)
    {
        // Remove scripts and style blocks
        html = Regex.Replace(html, @"<(script|style)[^>]*>[\s\S]*?</\1>",
            string.Empty, RegexOptions.IgnoreCase);
        // Remove HTML tags
        html = Regex.Replace(html, @"<[^>]+>", " ");
        // Collapse whitespace
        html = Regex.Replace(html, @"\s{2,}", " ");
        return html.Trim();
    }

    private async Task<Result<Material>> SaveMaterialAsync(
        Guid userId, Guid subjectId, string title, string sourcePath,
        MaterialFormat format, string text)
    {
        var material = new Material
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = subjectId,
            OriginalFileName = title,
            OriginalFormat = format,
            OriginalFilePath = sourcePath,
            KiContent = text,
            TokenCount = text.Length / 4, // rough token estimate
            CreatedAt = DateTime.UtcNow
        };

        _db.Materials.Add(material);
        await _db.SaveChangesAsync();
        return Result<Material>.Success(material);
    }

    private static string TryOcrPage(UglyToad.PdfPig.Content.Page page)
    {
        try
        {
            var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            if (!Directory.Exists(tessDataPath))
                return "[Image page - OCR skipped (tessdata missing)]";

            // OCR via Tesseract is available but requires image extraction from the PDF page
            // Returning placeholder until image extraction is wired to Pix
            return "[Image page - OCR placeholder]";
        }
        catch
        {
            return "[OCR error]";
        }
    }
}
