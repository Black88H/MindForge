using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class KnowledgeGraphService : IKnowledgeGraphService
{
    private readonly MindForgeDbContext _db;

    public KnowledgeGraphService(MindForgeDbContext db)
    {
        _db = db;
    }

    public async Task<KnowledgeNode> AddNodeAsync(Guid userId, Guid subjectId, string title, string summary)
    {
        var node = new KnowledgeNode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = subjectId,
            Title = title,
            Summary = summary,
            CreatedAt = DateTime.UtcNow
        };

        _db.KnowledgeNodes.Add(node);
        await _db.SaveChangesAsync();
        return node;
    }

    public async Task<KnowledgeEdge> AddEdgeAsync(Guid fromNodeId, Guid toNodeId, EdgeRelationType relationType)
    {
        // Check if edge already exists
        var existing = await _db.KnowledgeEdges
            .FirstOrDefaultAsync(e => e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId && e.RelationType == relationType);

        if (existing != null)
            return existing;

        var edge = new KnowledgeEdge
        {
            Id = Guid.NewGuid(),
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            RelationType = relationType,
            Strength = 1.0 // Default initial strength
        };

        _db.KnowledgeEdges.Add(edge);
        await _db.SaveChangesAsync();
        return edge;
    }

    public async Task<List<KnowledgeNode>> GetGraphAsync(Guid subjectId)
    {
        return await _db.KnowledgeNodes
            .Include(n => n.OutgoingEdges)
            .ThenInclude(e => e.ToNode)
            .Include(n => n.IncomingEdges)
            .Where(n => n.SubjectId == subjectId)
            .ToListAsync();
    }
}
