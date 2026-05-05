using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.AI.Providers;

namespace MindForge.Services.AI;

/// <summary>Returned by SearchAsync — one relevant text chunk with its source.</summary>
public record RelevantChunk(string MaterialName, string Text, float Score);

/// <summary>
/// Retrieval-Augmented Generation engine.
///
/// Flow:
///   1. IndexMaterialAsync — called when a material is added.  Splits content
///      into overlapping chunks, optionally generates Ollama embeddings, and
///      stores everything in the MaterialChunks table.
///   2. SearchAsync — called before each chat message.  Finds the top-K chunks
///      whose content best matches the user query via cosine similarity (if
///      embeddings exist) or TF-IDF keyword search (fallback).
/// </summary>
public class RAGService
{
    private readonly OllamaProvider _ollama;

    // ~300 tokens per chunk (1 token ≈ 4 chars) → precise retrieval with fewer tokens
    private const int ChunkChars   = 1200;
    private const int ChunkOverlap = 150;

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MindForge", "mindforge.db");

    public RAGService(OllamaProvider ollama) => _ollama = ollama;

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Chunk the material text, generate embeddings (if an embed model is available),
    /// and persist to the MaterialChunks table.  Called fire-and-forget in the background
    /// so it does not block the UI.
    /// </summary>
    public async Task IndexMaterialAsync(
        Guid   materialId,
        Guid   notebookId,
        string materialName,
        string content,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var chunks = MakeChunks(content);
        using var db = OpenDb();

        // Remove stale chunks for this material
        db.MaterialChunks.RemoveRange(db.MaterialChunks.Where(c => c.MaterialId == materialId));
        await db.SaveChangesAsync(ct);

        var embedModel = await FindEmbedModelAsync(ct);

        // Batch-embed all chunks in ONE API call instead of N separate calls
        List<float[]> embeddings = [];
        if (embedModel != null && chunks.Count > 0)
            embeddings = await _ollama.GetEmbeddingsBatchAsync(chunks, embedModel, ct);

        for (int i = 0; i < chunks.Count && !ct.IsCancellationRequested; i++)
        {
            var emb = i < embeddings.Count ? embeddings[i] : null;
            db.MaterialChunks.Add(new MaterialChunk
            {
                Id            = Guid.NewGuid(),
                MaterialId    = materialId,
                NotebookId    = notebookId,
                MaterialName  = materialName,
                ChunkIndex    = i,
                Text          = chunks[i],
                EmbeddingJson = emb != null ? JsonSerializer.Serialize(emb) : "[]",
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Retrieve the top-K chunks most relevant to the query.
    /// Uses cosine similarity on stored embeddings when available;
    /// falls back to TF-IDF keyword search otherwise.
    /// </summary>
    public async Task<List<RelevantChunk>> SearchAsync(
        string query,
        Guid   notebookId,
        int    topK = 8,
        CancellationToken ct = default)
    {
        using var db = OpenDb();
        var chunks = await db.MaterialChunks
            .Where(c => c.NotebookId == notebookId)
            .ToListAsync(ct);

        if (chunks.Count == 0) return [];

        // Vector search if any embeddings are stored
        bool hasEmbeddings = chunks.Any(c => c.EmbeddingJson.Length > 2);
        if (hasEmbeddings)
        {
            var embedModel = await FindEmbedModelAsync(ct);
            if (embedModel != null)
            {
                var queryVec = await _ollama.GetEmbeddingAsync(query, embedModel, ct);
                if (queryVec != null)
                {
                    var results = VectorSearch(queryVec, chunks, topK);
                    if (results.Count > 0) return results;
                }
            }
        }

        // BM25-style keyword search fallback
        return KeywordSearch(query, chunks, topK);
    }

    /// <summary>Delete all chunk data for a material (called on material deletion).</summary>
    public async Task DeleteMaterialChunksAsync(Guid materialId, CancellationToken ct = default)
    {
        using var db = OpenDb();
        db.MaterialChunks.RemoveRange(db.MaterialChunks.Where(c => c.MaterialId == materialId));
        await db.SaveChangesAsync(ct);
    }

    // ── Chunking ───────────────────────────────────────────────────────────────

    private static List<string> MakeChunks(string text)
    {
        var chunks = new List<string>();
        int pos    = 0;
        while (pos < text.Length)
        {
            var end = Math.Min(pos + ChunkChars, text.Length);

            // Try to break at a sentence boundary within the last 150 chars
            if (end < text.Length)
            {
                var breakPt = text.LastIndexOfAny(['.', '!', '?', '\n'], end,
                    Math.Min(150, end - pos));
                if (breakPt > pos) end = breakPt + 1;
            }

            var chunk = text[pos..end].Trim();
            if (chunk.Length > 60) chunks.Add(chunk);

            pos = Math.Max(pos + 1, end - ChunkOverlap);
        }
        return chunks;
    }

    // ── Vector search ──────────────────────────────────────────────────────────

    private static List<RelevantChunk> VectorSearch(
        float[]            queryVec,
        List<MaterialChunk> chunks,
        int topK)
    {
        var results = new List<(RelevantChunk chunk, float score)>();

        foreach (var c in chunks)
        {
            float[]? emb;
            try { emb = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson); }
            catch { emb = null; }

            if (emb is null || emb.Length != queryVec.Length) continue;

            float sim = CosineSimilarity(queryVec, emb);
            if (sim > 0.25f)
                results.Add((new RelevantChunk(c.MaterialName, c.Text, sim), sim));
        }

        return results
            .OrderByDescending(x => x.score)
            .Take(topK)
            .Select(x => x.chunk)
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot   += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return (float)(dot / (Math.Sqrt(normA) * Math.Sqrt(normB) + 1e-8));
    }

    // ── TF-IDF keyword fallback ────────────────────────────────────────────────

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "der","die","das","und","oder","ist","sind","wird","werden","bei","mit",
        "für","auf","von","aus","ein","eine","einem","einer","einen",
        "the","a","an","is","are","was","were","in","on","at","to","of","for",
        "it","its","be","by","this","that","as","not","but","from","with",
    };

    private static List<RelevantChunk> KeywordSearch(
        string query,
        List<MaterialChunk> chunks,
        int topK)
    {
        var terms = query
            .ToLower()
            .Split([' ',',','.','?','!',':',';','(',')','[',']'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2 && !StopWords.Contains(t))
            .Distinct()
            .ToList();

        if (terms.Count == 0)
            return chunks.Take(topK)
                .Select(c => new RelevantChunk(c.MaterialName, c.Text, 0.05f)).ToList();

        // IDF per term
        var idf = terms.ToDictionary(
            t => t,
            t => (float)Math.Log((chunks.Count + 1.0) /
                (chunks.Count(c => c.Text.Contains(t, StringComparison.OrdinalIgnoreCase)) + 1.0) + 1.0));

        return chunks
            .Select(c =>
            {
                var textLower = c.Text.ToLower();
                float score = terms.Sum(t =>
                {
                    int count = 0, idx = 0;
                    while ((idx = textLower.IndexOf(t, idx, StringComparison.Ordinal)) != -1)
                    { count++; idx += t.Length; }
                    float tf = count > 0 ? (float)(1 + Math.Log(count)) : 0f;
                    return tf * idf[t];
                });
                // Normalise by chunk length so shorter, denser chunks score higher
                float norm = score / (float)Math.Sqrt(Math.Max(1, c.Text.Length));
                return new RelevantChunk(c.MaterialName, c.Text, norm);
            })
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private MindForgeDbContext OpenDb() => new(
        new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseSqlite($"Data Source={DbPath}")
            .Options);

    private async Task<string?> FindEmbedModelAsync(CancellationToken ct)
    {
        try
        {
            var models = await _ollama.GetAvailableModelsAsync(ct);

            // Prefer dedicated embedding models
            string[] preferred = ["nomic-embed-text", "mxbai-embed-large", "all-minilm", "bge-m3", "bge-large"];
            foreach (var pref in preferred)
            {
                var hit = models.FirstOrDefault(m => m.Contains(pref, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit;
            }
            // Fall back to any available model (chat models can embed too, less accurate)
            return models.FirstOrDefault();
        }
        catch { return null; }
    }
}
