using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.AI;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class DocumentComparisonService : IDocumentComparisonService
{
    private readonly MindForgeDbContext _db;
    private readonly AISelector         _ai;

    public DocumentComparisonService(MindForgeDbContext db, AISelector ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<ComparisonResult> CompareAsync(Guid materialId1, Guid materialId2)
    {
        var mat1 = await _db.Materials.FindAsync(materialId1);
        var mat2 = await _db.Materials.FindAsync(materialId2);

        string doc1Name = mat1?.OriginalFileName ?? "Dokument 1";
        string doc2Name = mat2?.OriginalFileName ?? "Dokument 2";

        // Get representative chunks for each material
        var chunks1 = await _db.MaterialChunks
            .Where(c => c.MaterialId == materialId1)
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToListAsync();

        var chunks2 = await _db.MaterialChunks
            .Where(c => c.MaterialId == materialId2)
            .OrderBy(_ => Guid.NewGuid())
            .Take(5)
            .ToListAsync();

        // Fall back to KiContent if no chunks
        string text1 = chunks1.Any()
            ? string.Join("\n\n", chunks1.Select(c => c.Text))
            : (mat1?.KiContent ?? "");

        string text2 = chunks2.Any()
            ? string.Join("\n\n", chunks2.Select(c => c.Text))
            : (mat2?.KiContent ?? "");

        if (string.IsNullOrWhiteSpace(text1) && string.IsNullOrWhiteSpace(text2))
            return BuildFallbackComparison(doc1Name, doc2Name, "Keine Inhalte verfügbar.");

        var prompt =
            "You are an expert document analyst. Compare these two documents and identify similarities, differences, and insights.\n\n" +
            "DOCUMENT 1 (" + doc1Name + "):\n" +
            text1[..Math.Min(text1.Length, 2000)] + "\n\n" +
            "DOCUMENT 2 (" + doc2Name + "):\n" +
            text2[..Math.Min(text2.Length, 2000)] + "\n\n" +
            "Respond ONLY with a JSON object (no markdown):\n" +
            "{\n" +
            "  \"similarities\": [\n" +
            "    { \"topic\": \"...\", \"description\": \"...\", \"confidence\": 0.9 }\n" +
            "  ],\n" +
            "  \"differences\": [\n" +
            "    { \"aspect\": \"...\", \"doc1View\": \"...\", \"doc2View\": \"...\", \"significance\": \"high\" }\n" +
            "  ],\n" +
            "  \"complementaryInsights\": \"...\",\n" +
            "  \"recommendedReadingOrder\": \"...\"\n" +
            "}";

        try
        {
            var (provider, model) = await _ai.SelectAsync(AITask.Summarization);
            var json = await provider.GenerateAsync(model, prompt);

            var start = json.IndexOf('{');
            var end   = json.LastIndexOf('}');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];

            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;

            var similarities = new List<DocSimilarity>();
            if (root.TryGetProperty("similarities", out var simEl))
                foreach (var s in simEl.EnumerateArray())
                    similarities.Add(new DocSimilarity(
                        s.TryGetProperty("topic",       out var tp)  ? tp.GetString()  ?? "" : "",
                        s.TryGetProperty("description", out var ds)  ? ds.GetString()  ?? "" : "",
                        s.TryGetProperty("confidence",  out var cf)  ? cf.GetDouble()  : 0.7));

            var differences = new List<DocDifference>();
            if (root.TryGetProperty("differences", out var diffEl))
                foreach (var d in diffEl.EnumerateArray())
                    differences.Add(new DocDifference(
                        d.TryGetProperty("aspect",       out var asp) ? asp.GetString() ?? "" : "",
                        d.TryGetProperty("doc1View",     out var d1)  ? d1.GetString()  ?? "" : "",
                        d.TryGetProperty("doc2View",     out var d2)  ? d2.GetString()  ?? "" : "",
                        d.TryGetProperty("significance", out var sig) ? sig.GetString() ?? "medium" : "medium"));

            string complementary = root.TryGetProperty("complementaryInsights", out var ci)
                ? ci.GetString() ?? "" : "";
            string readingOrder  = root.TryGetProperty("recommendedReadingOrder", out var ro)
                ? ro.GetString() ?? "" : "";

            return new ComparisonResult(
                doc1Name, doc2Name,
                similarities.Count > 0 ? similarities : [new DocSimilarity("Allgemeines Thema", "Beide Dokumente behandeln verwandte Themen.", 0.5)],
                differences.Count > 0  ? differences  : [new DocDifference("Inhalt", "Dokument 1 Perspektive", "Dokument 2 Perspektive", "medium")],
                string.IsNullOrWhiteSpace(complementary) ? "Die Dokumente ergaenzen sich gegenseitig in ihren Perspektiven." : complementary,
                string.IsNullOrWhiteSpace(readingOrder)  ? "Beginne mit " + doc1Name + ", dann " + doc2Name + "." : readingOrder);
        }
        catch
        {
            return BuildFallbackComparison(doc1Name, doc2Name, "Vergleich konnte nicht generiert werden.");
        }
    }

    private static ComparisonResult BuildFallbackComparison(string doc1, string doc2, string reason)
        => new ComparisonResult(
            doc1, doc2,
            [new DocSimilarity("Gemeinsames Thema", reason, 0.5)],
            [new DocDifference("Nicht analysierbar", "-", "-", "low")],
            "Keine zusaetzlichen Erkenntnisse verfuegbar.",
            "Lies beide Dokumente in beliebiger Reihenfolge.");
}
