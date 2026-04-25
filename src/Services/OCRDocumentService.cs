using System.IO;
using MindForge.Models;
using Tesseract;

namespace MindForge.Services;

public class OCRDocumentService
{
    private readonly MindForgeDbContext _db;
    private static readonly string TessDataPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "tessdata");

    public OCRDocumentService(MindForgeDbContext db) => _db = db;

    public async Task<OCRDocument> ExtractFromImageAsync(string imagePath, string userId)
    {
        var doc = new OCRDocument
        {
            UserId = userId,
            UploadedFilePath = imagePath,
            Status = OCRStatus.Processing
        };
        _db.OCRDocuments.Add(doc);
        await _db.SaveChangesAsync();

        try
        {
            var (text, confidence) = await Task.Run(() => RunTesseract(imagePath));
            doc.ExtractedText = text;
            doc.Confidence = confidence;
            doc.Status = confidence >= 50 ? OCRStatus.Success : OCRStatus.PartialFail;
        }
        catch (Exception ex)
        {
            doc.ExtractedText = $"OCR-Fehler: {ex.Message}";
            doc.Confidence = 0;
            doc.Status = OCRStatus.Failed;
        }

        doc.ProcessedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return doc;
    }

    public async Task<OCRDocument> ExtractFromPDFAsync(string pdfPath, string userId)
    {
        // For PDFs, extract all pages as images first then OCR
        // For simplicity, treat PDF extraction as a text operation
        var doc = new OCRDocument
        {
            UserId = userId,
            UploadedFilePath = pdfPath,
            Status = OCRStatus.Processing
        };
        _db.OCRDocuments.Add(doc);
        await _db.SaveChangesAsync();

        try
        {
            // Try to read PDF as plain text if it's text-based
            var text = await Task.Run(() => ReadPdfAsText(pdfPath));
            doc.ExtractedText = text;
            doc.Confidence = string.IsNullOrWhiteSpace(text) ? 0 : 85;
            doc.Status = doc.Confidence >= 50 ? OCRStatus.Success : OCRStatus.PartialFail;
        }
        catch (Exception ex)
        {
            doc.ExtractedText = $"PDF-Fehler: {ex.Message}";
            doc.Status = OCRStatus.Failed;
        }

        doc.ProcessedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return doc;
    }

    public int GetConfidenceScore(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        // Heuristic: longer text with more words = higher confidence
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Math.Min(95, 40 + words.Length / 2);
    }

    public string AutoCorrectOCRText(string text, int confidence)
    {
        if (confidence >= 80) return text;
        // Basic auto-corrections for common OCR errors
        return text
            .Replace("0", "o", StringComparison.Ordinal)
            .Replace("|", "I", StringComparison.Ordinal);
    }

    private (string text, int confidence) RunTesseract(string imagePath)
    {
        if (!Directory.Exists(TessDataPath))
            return ("tessdata nicht gefunden. Bitte tessdata/ Ordner hinzufügen.", 0);

        using var engine = new TesseractEngine(TessDataPath, "deu+eng", EngineMode.Default);
        using var img = Pix.LoadFromFile(imagePath);
        using var page = engine.Process(img);
        var text = page.GetText();
        var confidence = (int)(page.GetMeanConfidence() * 100);
        return (text, confidence);
    }

    private static string ReadPdfAsText(string pdfPath)
    {
        // Simple byte extraction for text-based PDFs
        var bytes = File.ReadAllBytes(pdfPath);
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        var lines = new List<string>();
        int i = 0;
        while (i < content.Length - 4)
        {
            if (content[i] == '(' && content[i - 1] == 'j')
            {
                int end = content.IndexOf(')', i);
                if (end > i) lines.Add(content[(i + 1)..end]);
            }
            i++;
        }
        return lines.Count > 0 ? string.Join(" ", lines) : File.ReadAllText(pdfPath);
    }
}
