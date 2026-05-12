using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.AI;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class AnnotationService : IAnnotationService
{
    private readonly MindForgeDbContext _db;
    private readonly AISelector         _ai;

    public AnnotationService(MindForgeDbContext db, AISelector ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<Annotation> CreateAsync(
        Guid materialId, Guid userId,
        string selectedText, AnnotationType type,
        string userNote = "")
    {
        var aiNote = await GenerateSmartNoteAsync(selectedText, type);

        var annotation = new Annotation
        {
            Id           = Guid.NewGuid(),
            MaterialId   = materialId,
            UserId       = userId,
            SelectedText = selectedText,
            Type         = type,
            Color        = GetColorForType(type),
            AINote       = aiNote,
            UserNote     = userNote,
            CreatedAt    = DateTime.UtcNow
        };

        _db.Annotations.Add(annotation);
        await _db.SaveChangesAsync();
        return annotation;
    }

    public async Task<List<Annotation>> GetForMaterialAsync(Guid materialId)
        => await _db.Annotations
            .Where(a => a.MaterialId == materialId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

    public async Task<List<Annotation>> GetForUserAsync(Guid userId, AnnotationType? filter = null)
    {
        var query = _db.Annotations.Where(a => a.UserId == userId);
        if (filter.HasValue)
            query = query.Where(a => a.Type == filter.Value);
        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
    }

    public async Task DeleteAsync(Guid annotationId)
    {
        var ann = await _db.Annotations.FindAsync(annotationId);
        if (ann is not null)
        {
            _db.Annotations.Remove(ann);
            await _db.SaveChangesAsync();
        }
    }

    public async Task<string> GenerateSmartNoteAsync(string text, AnnotationType type)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var instruction = type switch
        {
            AnnotationType.Highlight  => "Fasse diesen markierten Text in einem Satz zusammen:",
            AnnotationType.Important  => "Erklaere, warum dieser Inhalt wichtig ist:",
            AnnotationType.Question   => "Formuliere eine Lernfrage basierend auf diesem Text:",
            AnnotationType.Concept    => "Definiere das Kernkonzept in diesem Text praegnant:",
            AnnotationType.Example    => "Erklaere, was dieses Beispiel veranschaulicht:",
            AnnotationType.Todo       => "Formuliere eine konkrete Aufgabe basierend auf diesem Text:",
            AnnotationType.Confusion  => "Erklaere diesen verwirrenden Aspekt einfach und klar:",
            _                         => "Fasse diesen Text kurz zusammen:"
        };

        var prompt = instruction + "\n\n\"" + text[..Math.Min(text.Length, 500)] + "\"\n\nAntworte auf Deutsch in 1-2 Saetzen.";

        try
        {
            var (provider, model) = await _ai.SelectAsync(AITask.Summarization);
            var result = await provider.GenerateAsync(model, prompt);
            return result.Trim();
        }
        catch
        {
            return type switch
            {
                AnnotationType.Question  => "Was bedeutet dieser Abschnitt im Kontext des Gesamtthemas?",
                AnnotationType.Concept   => "Kernkonzept: " + text[..Math.Min(text.Length, 80)],
                _                        => text[..Math.Min(text.Length, 100)]
            };
        }
    }

    private static string GetColorForType(AnnotationType type) => type switch
    {
        AnnotationType.Highlight => "#FBBF24",   // amber
        AnnotationType.Important => "#EF4444",   // red
        AnnotationType.Question  => "#3B82F6",   // blue
        AnnotationType.Concept   => "#8B5CF6",   // purple
        AnnotationType.Example   => "#10B981",   // green
        AnnotationType.Todo      => "#F97316",   // orange
        AnnotationType.Confusion => "#6B7280",   // gray
        _                        => "#FBBF24"
    };
}
