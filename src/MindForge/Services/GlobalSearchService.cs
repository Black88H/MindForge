using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class GlobalSearchService : IGlobalSearchService
{
    private readonly MindForgeDbContext _db;

    public GlobalSearchService(MindForgeDbContext db) => _db = db;

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var q      = query.Trim().ToLower();
        var userId = MindForge.Helpers.UserSession.UserId;
        var results = new List<SearchResult>();

        // ── Notebooks ────────────────────────────────────────────────────────
        var notebooks = await _db.Notebooks
            .Where(n => n.UserId == userId && n.Name.ToLower().Contains(q))
            .Take(5).ToListAsync();

        results.AddRange(notebooks.Select(n => new SearchResult(
            "Notebook", n.Id, n.Name,
            $"Lernlevel: {n.LearningLevel} · Stil: {n.ExplanationStyle}",
            "📓")));

        // ── Materials ────────────────────────────────────────────────────────
        var materials = await _db.Materials
            .Where(m => m.UserId == userId &&
                       (m.OriginalFileName.ToLower().Contains(q) ||
                        m.KiContent.ToLower().Contains(q)))
            .Take(5).ToListAsync();

        results.AddRange(materials.Select(m => new SearchResult(
            "Material", m.Id, m.OriginalFileName,
            Truncate(m.KiContent, 120),
            "📄")));

        // ── Chat messages ────────────────────────────────────────────────────
        var chats = await _db.ChatMessages
            .Where(c => c.UserId == userId && c.Content.ToLower().Contains(q))
            .OrderByDescending(c => c.CreatedAt)
            .Take(5).ToListAsync();

        results.AddRange(chats.Select(c => new SearchResult(
            "Chat", c.Id,
            $"Chat · {c.CreatedAt:dd.MM.yy HH:mm}",
            Truncate(c.Content, 120),
            "💬")));

        // ── Pre-built search index (avoids duplicates) ───────────────────────
        var knownIds = results.Select(r => r.EntityId).ToHashSet();
        var indexed  = await _db.SearchIndexes
            .Where(s => s.UserId == userId &&
                       (s.Title.ToLower().Contains(q) || s.Snippet.ToLower().Contains(q)))
            .Take(5).ToListAsync();

        results.AddRange(indexed
            .Where(s => !knownIds.Contains(s.EntityId))
            .Select(s => new SearchResult(s.EntityType, s.EntityId, s.Title, s.Snippet, "🔍")));

        return results;
    }

    public async Task RebuildIndexAsync()
    {
        var userId  = MindForge.Helpers.UserSession.UserId;
        var existing = await _db.SearchIndexes.Where(s => s.UserId == userId).ToListAsync();
        _db.SearchIndexes.RemoveRange(existing);

        var entries = new List<SearchIndex>();

        var notebooks = await _db.Notebooks.Where(n => n.UserId == userId).ToListAsync();
        entries.AddRange(notebooks.Select(n => new SearchIndex
        {
            Id         = Guid.NewGuid(),
            EntityType = "Notebook",
            EntityId   = n.Id,
            Title      = n.Name,
            Snippet    = $"Lernlevel: {n.LearningLevel} · Stil: {n.ExplanationStyle}",
            UserId     = userId,
            IndexedAt  = DateTime.UtcNow
        }));

        var materials = await _db.Materials.Where(m => m.UserId == userId).ToListAsync();
        entries.AddRange(materials.Select(m => new SearchIndex
        {
            Id         = Guid.NewGuid(),
            EntityType = "Material",
            EntityId   = m.Id,
            Title      = m.OriginalFileName,
            Snippet    = Truncate(m.KiContent, 300),
            UserId     = userId,
            IndexedAt  = DateTime.UtcNow
        }));

        _db.SearchIndexes.AddRange(entries);
        await _db.SaveChangesAsync();
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + "…";
}
