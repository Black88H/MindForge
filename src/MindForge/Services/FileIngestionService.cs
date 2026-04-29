using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using Tesseract;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MindForge.Services;

public class FileIngestionService : IFileIngestionService
{
    private readonly MindForgeDbContext _db;
    
    public FileIngestionService(MindForgeDbContext db)
    {
        _db = db;
    }

    public async Task<Result<Material>> IngestPdfAsync(Guid userId, Guid subjectId, string filePath, string title)
    {
        if (!File.Exists(filePath))
            return Result<Material>.Failure("Datei nicht gefunden.");

        try
        {
            var text = await ExtractTextFromPdfAsync(filePath);
            
            var material = new Material
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SubjectId = subjectId,
                OriginalFileName = title,
                OriginalFormat = MaterialFormat.PDF,
                OriginalFilePath = filePath,
                KiContent = text,
                CreatedAt = DateTime.UtcNow
            };
            
            _db.Materials.Add(material);
            await _db.SaveChangesAsync();
            
            return Result<Material>.Success(material);
        }
        catch (Exception ex)
        {
            return Result<Material>.Failure($"Fehler beim Einlesen: {ex.Message}");
        }
    }

    public Task<string> ExtractTextFromPdfAsync(string filePath)
    {
        return Task.Run(() => 
        {
            var sb = new StringBuilder();
            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    var text = page.Text;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        // Falls die Seite nur ein Bild ist -> OCR mit Tesseract
                        text = TryOcrPage(page);
                    }
                    sb.AppendLine(text);
                }
            }
            return sb.ToString();
        });
    }

    private string TryOcrPage(UglyToad.PdfPig.Content.Page page)
    {
        try 
        {
            // Achtung: Für Tesseract müssen die "tessdata" Ordner mit den Sprachdateien (deu.traineddata) existieren!
            var tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            if (!Directory.Exists(tessDataPath))
                return "[Bild - OCR fehlgeschlagen (tessdata fehlt)]";

            // Normalerweise muesste hier das Bild aus `page.GetImages()` extrahiert werden 
            // und als Pix Objekt geladen werden.
            
            // using var engine = new TesseractEngine(tessDataPath, "deu", EngineMode.Default);
            // using var img = Pix.LoadFromFile("temp_image.png"); 
            // using var p = engine.Process(img);
            // return p.GetText();
            return "[Bild erkannt - OCR Platzhalter]";
        }
        catch
        {
            return "[Fehler bei OCR]";
        }
    }
}
