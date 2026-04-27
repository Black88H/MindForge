using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Tesseract;
using UglyToad.PdfPig;
using MindForge.Models;

namespace MindForge.Services;

public interface IFileIngestionService
{
    Task<Material> IngestFileAsync(string filePath, Guid subjectId, Guid userId);
}

public class FileIngestionService : IFileIngestionService
{
    private readonly MindForgeDbContext _db;
    private readonly ILogger<FileIngestionService> _logger;
    private readonly string _materialsDir;
    private readonly string _tessdataDir;

    public FileIngestionService(MindForgeDbContext db, ILogger<FileIngestionService> logger)
    {
        _db = db;
        _logger = logger;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _materialsDir = Path.Combine(appData, "MindForge", "materials");
        _tessdataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "tessdata");
    }

    public async Task<Material> IngestFileAsync(string filePath, Guid subjectId, Guid userId)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("Datei nicht gefunden", filePath);

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var format = ext switch
        {
            ".pdf" => MaterialFormat.PDF,
            ".docx" => MaterialFormat.DOCX,
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff" => MaterialFormat.Image,
            _ => throw new NotSupportedException($"Format {ext} wird nicht unterstützt")
        };

        var rawText = format switch
        {
            MaterialFormat.PDF => ExtractTextFromPdf(filePath),
            MaterialFormat.DOCX => ExtractTextFromDocx(filePath),
            MaterialFormat.Image or MaterialFormat.Handwriting => ExtractTextFromImage(filePath),
            _ => throw new NotSupportedException()
        };

        var kiContent = ConvertToKiMarkdown(rawText, Path.GetFileName(filePath), format);
        var hash = ComputeHash(kiContent);

        // Kopiere Originaldatei in App-Verzeichnis
        var destDir = Path.Combine(_materialsDir, userId.ToString(), subjectId.ToString());
        Directory.CreateDirectory(destDir);
        var destFile = Path.Combine(destDir, $"{Guid.NewGuid()}_{Path.GetFileName(filePath)}");
        File.Copy(filePath, destFile, overwrite: true);

        var material = new Material
        {
            SubjectId = subjectId,
            UserId = userId,
            OriginalFileName = Path.GetFileName(filePath),
            OriginalFormat = format,
            OriginalFilePath = destFile,
            KiContent = kiContent,
            KiContentHash = hash,
            TokenCount = EstimateTokenCount(kiContent)
        };

        _db.Materials.Add(material);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Material ingested: {FileName} → {TokenCount} tokens", material.OriginalFileName, material.TokenCount);
        return material;
    }

    private string ExtractTextFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(filePath);
        foreach (var page in doc.GetPages())
        {
            sb.AppendLine(page.Text);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private string ExtractTextFromDocx(string filePath)
    {
        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document.Body;
        if (body == null) return string.Empty;

        foreach (var para in body.Elements<Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }
        return sb.ToString();
    }

    private string ExtractTextFromImage(string filePath)
    {
        using var engine = new TesseractEngine(_tessdataDir, "deu+eng", EngineMode.Default);
        using var img = Pix.LoadFromFile(filePath);
        using var page = engine.Process(img);
        return page.GetText();
    }

    private static string ConvertToKiMarkdown(string rawText, string fileName, MaterialFormat format)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"source_file: \"{fileName}\"");
        sb.AppendLine($"format: \"{format}\"");
        sb.AppendLine($"extracted_at: \"{DateTime.UtcNow:O}\"");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {Path.GetFileNameWithoutExtension(fileName)}");
        sb.AppendLine();
        sb.AppendLine(rawText.Trim());
        return sb.ToString();
    }

    private static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static int EstimateTokenCount(string text)
    {
        // Grobe Schätzung: ~4 Zeichen pro Token (Deutsch hat längere Wörter)
        return text.Length / 4;
    }
}
