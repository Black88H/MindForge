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

public class AIConceptGraphService : IAIConceptGraphService
{
    private readonly MindForgeDbContext _db;
    private readonly AISelector         _ai;

    public AIConceptGraphService(MindForgeDbContext db, AISelector ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<ConceptGraphData> GenerateAsync(Guid notebookId)
    {
        var chunks = await _db.MaterialChunks
            .Where(c => c.NotebookId == notebookId)
            .OrderBy(_ => Guid.NewGuid())
            .Take(8)
            .ToListAsync();

        var context = string.Join("\n\n", chunks.Select(c => c.Text));
        if (string.IsNullOrWhiteSpace(context))
        {
            var nb = await _db.Notebooks.FindAsync(notebookId);
            context = nb?.Name ?? "General";
        }

        var prompt =
            "You are an expert knowledge engineer. Extract the key concepts and their relationships from the text below.\n\n" +
            "TEXT:\n" +
            context[..Math.Min(context.Length, 4000)] + "\n\n" +
            "Respond ONLY with a JSON object (no markdown, no extra text):\n" +
            "{\n" +
            "  \"nodes\": [\n" +
            "    { \"id\": \"n1\", \"label\": \"Concept Name\", \"type\": \"concept\", \"importance\": 0.9, \"description\": \"Short description\" }\n" +
            "  ],\n" +
            "  \"edges\": [\n" +
            "    { \"source\": \"n1\", \"target\": \"n2\", \"type\": \"relates-to\", \"strength\": 0.8 }\n" +
            "  ]\n" +
            "}\n\n" +
            "Rules:\n" +
            "- Extract 6-15 key concepts as nodes\n" +
            "- importance: 0.0-1.0 (main concepts > 0.7, sub-concepts < 0.5)\n" +
            "- type options: concept, definition, process, example, principle\n" +
            "- edge type options: relates-to, leads-to, is-a, part-of, causes, contrasts-with\n" +
            "- strength: 0.0-1.0 (strong relationship > 0.7)\n" +
            "- Only connect nodes that have genuine conceptual relationships";

        ConceptGraphData graph;
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
            var nodes      = new List<GraphNode>();
            var edges      = new List<GraphEdge>();

            if (root.TryGetProperty("nodes", out var nodesEl))
                foreach (var n in nodesEl.EnumerateArray())
                    nodes.Add(new GraphNode(
                        n.TryGetProperty("id",          out var id)   ? id.GetString()          ?? Guid.NewGuid().ToString("N")[..6] : Guid.NewGuid().ToString("N")[..6],
                        n.TryGetProperty("label",       out var lb)   ? lb.GetString()          ?? "?"  : "?",
                        n.TryGetProperty("type",        out var tp)   ? tp.GetString()          ?? "concept" : "concept",
                        n.TryGetProperty("importance",  out var imp)  ? imp.GetDouble()         : 0.5,
                        n.TryGetProperty("description", out var desc) ? desc.GetString()        ?? "" : ""));

            if (root.TryGetProperty("edges", out var edgesEl))
                foreach (var e in edgesEl.EnumerateArray())
                    edges.Add(new GraphEdge(
                        e.TryGetProperty("source",   out var src) ? src.GetString()  ?? "" : "",
                        e.TryGetProperty("target",   out var tgt) ? tgt.GetString()  ?? "" : "",
                        e.TryGetProperty("type",     out var et)  ? et.GetString()   ?? "relates-to" : "relates-to",
                        e.TryGetProperty("strength", out var str) ? str.GetDouble()  : 0.5));

            graph = nodes.Count > 0
                ? new ConceptGraphData(nodes, edges)
                : BuildFallbackGraph(context);
        }
        catch
        {
            graph = BuildFallbackGraph(context);
        }

        // Persist to DB
        var serialized = JsonSerializer.Serialize(graph);
        var existing   = await _db.ConceptGraphs
            .FirstOrDefaultAsync(g => g.NotebookId == notebookId);

        if (existing is not null)
        {
            existing.GraphJson     = serialized;
            existing.GeneratedAt   = DateTime.UtcNow;
            existing.NodeCount     = graph.Nodes.Count;
        }
        else
        {
            _db.ConceptGraphs.Add(new ConceptGraph
            {
                Id          = Guid.NewGuid(),
                NotebookId  = notebookId,
                GraphJson   = serialized,
                GeneratedAt = DateTime.UtcNow,
                NodeCount   = graph.Nodes.Count
            });
        }
        await _db.SaveChangesAsync();

        return graph;
    }

    public async Task<ConceptGraphData?> GetLatestAsync(Guid notebookId)
    {
        var record = await _db.ConceptGraphs
            .Where(g => g.NotebookId == notebookId)
            .OrderByDescending(g => g.GeneratedAt)
            .FirstOrDefaultAsync();

        if (record is null) return null;

        try
        {
            return JsonSerializer.Deserialize<ConceptGraphData>(record.GraphJson);
        }
        catch
        {
            return null;
        }
    }

    // ── Fallback ──────────────────────────────────────────────────────────────

    private static ConceptGraphData BuildFallbackGraph(string context)
    {
        // Extract simple word frequencies as a basic fallback
        var words = context
            .Split(' ', '\n', '\r', '\t', '.', ',', '!', '?', ':', ';')
            .Select(w => w.Trim().ToLower())
            .Where(w => w.Length > 4)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(6)
            .ToList();

        var nodes = words.Select((g, i) => new GraphNode(
            $"n{i}", g.Key, "concept", Math.Min(1.0, g.Count() / 10.0), "")).ToList();

        var edges = new List<GraphEdge>();
        for (int i = 0; i < nodes.Count - 1; i++)
            edges.Add(new GraphEdge(nodes[i].Id, nodes[i + 1].Id, "relates-to", 0.5));

        return new ConceptGraphData(nodes, edges);
    }
}
