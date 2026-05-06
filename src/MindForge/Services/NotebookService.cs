using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.AI;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class NotebookService : INotebookService
{
    private readonly MindForgeDbContext _db;
    private readonly AISelector _ai;
    private const int MaxContextChars = 6000;

    public NotebookService(MindForgeDbContext db, AISelector ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<string> SummarizeMaterialAsync(Guid materialId, CancellationToken ct = default)
    {
        var material = await _db.Materials.FindAsync([materialId], ct)
            ?? throw new ArgumentException($"Material {materialId} not found.");

        var content = Truncate(material.KiContent, MaxContextChars);
        var prompt = $"""
            Summarize the following document in a clear, structured way.
            Use bullet points for key concepts. Keep it concise (max 300 words).
            Document title: {material.OriginalFileName}

            Content:
            {content}
            """;

        var (provider, model) = await _ai.SelectAsync(AITask.Summarization, ct: ct);
        return await provider.GenerateAsync(model, prompt, ct);
    }

    public async Task<string> SummarizeSubjectAsync(Guid subjectId, int maxMaterials = 5, CancellationToken ct = default)
    {
        var materials = await _db.Materials
            .Where(m => m.SubjectId == subjectId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(maxMaterials)
            .ToListAsync(ct);

        if (materials.Count == 0)
            return "No materials found for this subject.";

        var combined = new StringBuilder();
        foreach (var m in materials)
        {
            combined.AppendLine($"## {m.OriginalFileName}");
            combined.AppendLine(Truncate(m.KiContent, MaxContextChars / materials.Count));
            combined.AppendLine();
        }

        var prompt = $"""
            You are summarizing a collection of study materials for a subject.
            Provide a comprehensive overview covering all major topics.
            Use clear headings, bullet points, and highlight key terms.

            Materials:
            {combined}
            """;

        var (provider, model) = await _ai.SelectAsync(AITask.Summarization, ct: ct);
        return await provider.GenerateAsync(model, prompt, ct);
    }

    public async Task<CitedAnswer> AskWithSourcesAsync(Guid userId, string question,
        IList<Guid> materialIds, CancellationToken ct = default)
    {
        List<(Guid Id, string Title, string Content)> materials;

        if (materialIds.Count > 0)
        {
            materials = await _db.Materials
                .Where(m => materialIds.Contains(m.Id))
                .Select(m => new { m.Id, m.OriginalFileName, m.KiContent })
                .AsAsyncEnumerable()
                .Select(m => (m.Id, m.OriginalFileName, m.KiContent))
                .ToListAsync(ct);
        }
        else
        {
            materials = await _db.Materials
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(3)
                .Select(m => new { m.Id, m.OriginalFileName, m.KiContent })
                .AsAsyncEnumerable()
                .Select(m => (m.Id, m.OriginalFileName, m.KiContent))
                .ToListAsync(ct);
        }

        if (materials.Count == 0)
        {
            return new CitedAnswer("I don't have any documents to answer from. Please upload materials first.", []);
        }

        var context = new StringBuilder();
        var citations = new List<Citation>();

        foreach (var m in materials)
        {
            var excerpt = Truncate(m.Content, MaxContextChars / materials.Count);
            context.AppendLine($"[Source: {m.Title}]");
            context.AppendLine(excerpt);
            context.AppendLine();

            var shortExcerpt = excerpt.Length > 200
                ? excerpt[..200].TrimEnd() + "..."
                : excerpt;
            citations.Add(new Citation(m.Id, m.Title, shortExcerpt));
        }

        var prompt = $"""
            Answer the following question based ONLY on the provided sources.
            After your answer, list which sources you used.
            If the answer is not in the sources, say so clearly.

            Question: {question}

            Sources:
            {context}
            """;

        var (provider, model) = await _ai.SelectAsync(AITask.QnA, ct: ct);
        var answer = await provider.GenerateAsync(model, prompt, ct);

        // Filter citations to only those mentioned in the answer
        var usedCitations = citations
            .Where(c => answer.Contains(c.MaterialTitle, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (usedCitations.Count == 0) usedCitations = citations;

        return new CitedAnswer(answer, usedCitations);
    }

    public async Task<string> GenerateStudyGuideAsync(Guid subjectId, CancellationToken ct = default)
    {
        var materials = await _db.Materials
            .Where(m => m.SubjectId == subjectId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .ToListAsync(ct);

        if (materials.Count == 0)
            return "No materials found. Please add documents to this subject first.";

        var combined = new StringBuilder();
        foreach (var m in materials)
        {
            combined.AppendLine($"## {m.OriginalFileName}");
            combined.AppendLine(Truncate(m.KiContent, 1500));
            combined.AppendLine();
        }

        var prompt = $"""
            Create a comprehensive study guide based on the following materials.
            Structure it as:
            1. Key Concepts (bullet points)
            2. Important Definitions
            3. Core Theories / Frameworks
            4. Common Exam Questions (with brief answers)
            5. Summary

            Materials:
            {combined}
            """;

        var (provider, model) = await _ai.SelectAsync(AITask.StudyGuide, ct: ct);
        return await provider.GenerateAsync(model, prompt, ct);
    }

    public async Task<List<string>> GenerateTopicListAsync(Guid materialId, CancellationToken ct = default)
    {
        var material = await _db.Materials.FindAsync([materialId], ct)
            ?? throw new ArgumentException($"Material {materialId} not found.");

        var content = Truncate(material.KiContent, MaxContextChars);
        var prompt = $"""
            Extract a list of main topics from this document.
            Return ONLY a numbered list, one topic per line, no explanations.

            Document: {material.OriginalFileName}
            Content:
            {content}
            """;

        var (provider, model) = await _ai.SelectAsync(AITask.Summarization, ct: ct);
        var response = await provider.GenerateAsync(model, prompt, ct);

        return response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ' ', '-'))
            .Where(l => l.Length > 2)
            .ToList();
    }

    public async Task<string> GenerateAudioOverviewAsync(Guid subjectId, CancellationToken ct = default)
    {
        var summary = await SummarizeSubjectAsync(subjectId, 3, ct);

        var prompt = $"""
            Convert this study summary into a natural spoken-word audio script.
            Write it as if a teacher is speaking to a student.
            Use conversational language, clear transitions, and summarize all key points.
            Keep it under 500 words.

            Summary:
            {summary}
            """;

        var (provider, model) = await _ai.SelectAsync(AITask.Summarization, ct: ct);
        return await provider.GenerateAsync(model, prompt, ct);
    }

    public Task SpeakTextAsync(string text, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            using var synth = new System.Speech.Synthesis.SpeechSynthesizer();
            synth.SetOutputToDefaultAudioDevice();
            synth.Rate = 0;
            synth.Volume = 100;

            using var reg = ct.Register(synth.SpeakAsyncCancelAll);
            synth.Speak(text);
        }, ct);
    }

    private static string Truncate(string text, int maxChars)
    {
        if (text.Length <= maxChars) return text;
        return text[..maxChars] + "\n[... content truncated ...]";
    }
}
