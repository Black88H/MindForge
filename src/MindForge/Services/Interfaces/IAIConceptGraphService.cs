using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public record GraphNode(string Id, string Label, string Type, double Importance, string Description);
public record GraphEdge(string Source, string Target, string Type, double Strength);
public record ConceptGraphData(List<GraphNode> Nodes, List<GraphEdge> Edges);

public interface IAIConceptGraphService
{
    Task<ConceptGraphData> GenerateAsync(Guid notebookId);
    Task<ConceptGraphData?> GetLatestAsync(Guid notebookId);
}
