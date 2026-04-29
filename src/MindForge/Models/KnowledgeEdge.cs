using System;
namespace MindForge.Models;
public class KnowledgeEdge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromNodeId { get; set; }
    public KnowledgeNode? FromNode { get; set; }
    public Guid ToNodeId { get; set; }
    public KnowledgeNode? ToNode { get; set; }
    public EdgeRelationType RelationType { get; set; }
    public double Strength { get; set; } = 0.5;
}
