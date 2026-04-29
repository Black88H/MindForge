using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public interface IKnowledgeGraphService
{
    Task<KnowledgeNode> AddNodeAsync(Guid userId, Guid subjectId, string title, string summary);
    Task<KnowledgeEdge> AddEdgeAsync(Guid fromNodeId, Guid toNodeId, EdgeRelationType relationType);
    Task<List<KnowledgeNode>> GetGraphAsync(Guid subjectId);
}
