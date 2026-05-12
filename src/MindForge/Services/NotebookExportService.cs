using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class NotebookExportService : INotebookExportService
{
    private readonly MindForgeDbContext _db;

    public NotebookExportService(MindForgeDbContext db) => _db = db;

    public async Task<string> ExportAsync(Guid notebookId, string destinationFolder)
    {
        var notebook = await _db.Notebooks.FindAsync(notebookId)
            ?? throw new InvalidOperationException("Notebook nicht gefunden.");

        var materials = await _db.Materials
            .Where(m => m.NotebookId == notebookId)
            .ToListAsync();

        var chats = await _db.ChatMessages
            .Where(c => c.NotebookId == notebookId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var chunks = await _db.MaterialChunks
            .Where(c => c.NotebookId == notebookId)
            .ToListAsync();

        // Compose export payload
        var payload = new
        {
            Version    = "8.0.0",
            ExportedAt = DateTime.UtcNow,
            Notebook   = notebook,
            Materials  = materials,
            Chats      = chats.Select(c => new { c.Role, c.Content, c.CreatedAt }),
            Chunks     = chunks.Select(c => new { c.MaterialId, c.ChunkIndex, c.Text })
        };

        var json     = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        var safeName = string.Concat(notebook.Name.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
        var zipPath  = Path.Combine(destinationFolder,
            $"{safeName}_{DateTime.Now:yyyyMMdd_HHmm}.mindforge");

        Directory.CreateDirectory(destinationFolder);

        using var zip    = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var dataEntry    = zip.CreateEntry("notebook.json");
        using var writer = new StreamWriter(dataEntry.Open());
        await writer.WriteAsync(json);

        return zipPath;
    }

    public async Task<Guid> ImportAsync(string zipFilePath, Guid userId)
    {
        if (!File.Exists(zipFilePath))
            throw new FileNotFoundException("Exportdatei nicht gefunden.", zipFilePath);

        using var zip = ZipFile.OpenRead(zipFilePath);
        var dataEntry = zip.GetEntry("notebook.json")
            ?? throw new InvalidDataException("Ungültige .mindforge-Datei.");

        string json;
        using (var reader = new StreamReader(dataEntry.Open()))
            json = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var root      = doc.RootElement;

        // Reconstruct notebook with a new Id to avoid conflicts
        var nbEl = root.GetProperty("Notebook");
        var newId = Guid.NewGuid();

        var notebook = new Notebook
        {
            Id               = newId,
            UserId           = userId,
            SubjectId        = Guid.TryParse(nbEl.GetProperty("SubjectId").GetString(), out var sid) ? sid : Guid.NewGuid(),
            Name             = nbEl.GetProperty("Name").GetString() + " (Importiert)" ?? "(Importiert)",
            LearningLevel    = nbEl.TryGetProperty("LearningLevel", out var ll) ? ll.GetString() ?? "Fortgeschritten" : "Fortgeschritten",
            ExplanationStyle = nbEl.TryGetProperty("ExplanationStyle", out var es) ? es.GetString() ?? "Normal" : "Normal",
            Language         = nbEl.TryGetProperty("Language", out var lang) ? lang.GetString() ?? "Deutsch" : "Deutsch",
            CreatedAt        = DateTime.UtcNow,
            LastModified     = DateTime.UtcNow
        };
        _db.Notebooks.Add(notebook);

        // Reconstruct chat messages
        if (root.TryGetProperty("Chats", out var chatsEl))
        {
            foreach (var c in chatsEl.EnumerateArray())
            {
                _db.ChatMessages.Add(new ChatMessage
                {
                    Id         = Guid.NewGuid(),
                    UserId     = userId,
                    NotebookId = newId,
                    Role       = Enum.TryParse<ChatRole>(c.GetProperty("Role").GetString(), out var role) ? role : ChatRole.User,
                    Content    = c.GetProperty("Content").GetString() ?? string.Empty,
                    CreatedAt  = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        return newId;
    }
}
