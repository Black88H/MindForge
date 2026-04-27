using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MindForge.Models;

public enum EdgeRelationType
{
    Prerequisite, RelatedTo, PartOf, Contradicts
}

public class KnowledgeEdge
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid FromNodeId { get; set; }
    [ForeignKey(nameof(FromNodeId))]
    public KnowledgeNode? FromNode { get; set; }
    
    public Guid ToNodeId { get; set; }
    [ForeignKey(nameof(ToNodeId))]
    public KnowledgeNode? ToNode { get; set; }
    
    public EdgeRelationType RelationType { get; set; }
    public float Strength { get; set; } = 0.5f;
}
