using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindForge.Models;
using MindForge.Services.AI.Interfaces;

namespace MindForge.Services;

public interface IKnowledgeGraphService
{
    Task<List<KnowledgeNode>> BuildGraphFromMaterialAsync(Guid materialId);
    Task<List<KnowledgeNode>> GetNodesForSubjectAsync(Guid subjectId, Guid userId);
    Task<List<KnowledgeEdge>> GetEdgesForSubjectAsync(Guid subjectId, Guid userId);
    Task<List<KnowledgeNode>> GetWeakNodesAsync(Guid userId, Guid subjectId, float threshold = 0.5f);
    Task UpdateMasteryAsync(Guid nodeId, float newMastery);
}

public class KnowledgeGraphService : IKnowledgeGraphService
{
    private readonly MindForgeDbContext _db;
    private readonly IAISelector _ai;
    private readonly ILogger<KnowledgeGraphService> _logger;

    public KnowledgeGraphService(MindForgeDbContext db, IAISelector ai, ILogger<KnowledgeGraphService> logger)
    {
        _db = db;
        _ai = ai;
        _logger = logger;
    }

    public async Task<List<KnowledgeNode>> BuildGraphFromMaterialAsync(Guid materialId)
    {
        var material = await _db.Materials.FindAsync(materialId)
            ?? throw new ArgumentException("Material nicht gefunden");

        var prompt = $@"Analysiere den folgenden Lerninhalt und extrahiere die Kernkonzepte.
Antworte NUR mit einem JSON-Array. Jedes Element hat:
- ""title"": Konzeptname (max 200 Zeichen)
- ""summary"": Kurze Erklärung (2-3 Sätze)
- ""prerequisites"": Array von Konzeptnamen die Voraussetzung sind

Inhalt:
{material.KiContent}

Antworte NUR mit validem JSON, kein Markdown, kein Text davor oder danach.";

        var response = await _ai.ExecuteAsync(AI.Models.TaskType.ContentGeneration, prompt);
        
        var nodes = new List<KnowledgeNode>();
        try
        {
            var concepts = JsonSerializer.Deserialize<List<ConceptDto>>(response.Content, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            foreach (var concept in concepts)
            {
                var node = new KnowledgeNode
                {
                    UserId = material.UserId,
                    SubjectId = material.SubjectId,
                    Title = concept.Title ?? "Unbenannt",
                    Summary = concept.Summary ?? "",
                    MaterialIds = materialId.ToString()
                };
                _db.KnowledgeNodes.Add(node);
                nodes.Add(node);
            }
            await _db.SaveChangesAsync();

            // Edges erstellen basierend auf Prerequisites
            foreach (var (concept, index) in concepts.Select((c, i) => (c, i)))
            {
                if (concept.Prerequisites == null) continue;
                foreach (var prereqTitle in concept.Prerequisites)
                {
                    var fromNode = nodes.FirstOrDefault(n => 
                        n.Title.Equals(prereqTitle, StringComparison.OrdinalIgnoreCase));
                    if (fromNode != null)
                    {
                        _db.KnowledgeEdges.Add(new KnowledgeEdge
                        {
                            FromNodeId = fromNode.Id,
                            ToNodeId = nodes[index].Id,
                            RelationType = EdgeRelationType.Prerequisite,
                            Strength = 0.8f
                        });
                    }
                }
            }
            await _db.SaveChangesAsync();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "KI-Response konnte nicht als JSON geparsed werden");
        }

        return nodes;
    }

    public async Task<List<KnowledgeNode>> GetNodesForSubjectAsync(Guid subjectId, Guid userId)
    {
        return await _db.KnowledgeNodes
            .Where(n => n.SubjectId == subjectId && n.UserId == userId)
            .OrderBy(n => n.Title)
            .ToListAsync();
    }

    public async Task<List<KnowledgeEdge>> GetEdgesForSubjectAsync(Guid subjectId, Guid userId)
    {
        var nodeIds = await _db.KnowledgeNodes
            .Where(n => n.SubjectId == subjectId && n.UserId == userId)
            .Select(n => n.Id)
            .ToListAsync();

        return await _db.KnowledgeEdges
            .Where(e => nodeIds.Contains(e.FromNodeId) && nodeIds.Contains(e.ToNodeId))
            .ToListAsync();
    }

    public async Task<List<KnowledgeNode>> GetWeakNodesAsync(Guid userId, Guid subjectId, float threshold = 0.5f)
    {
        return await _db.KnowledgeNodes
            .Where(n => n.UserId == userId && n.SubjectId == subjectId && n.MasteryLevel < threshold)
            .OrderBy(n => n.MasteryLevel)
            .ToListAsync();
    }

    public async Task UpdateMasteryAsync(Guid nodeId, float newMastery)
    {
        var node = await _db.KnowledgeNodes.FindAsync(nodeId);
        if (node != null)
        {
            node.MasteryLevel = Math.Clamp(newMastery, 0f, 1f);
            node.LastReviewed = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    private record ConceptDto(string? Title, string? Summary, List<string>? Prerequisites);
}
