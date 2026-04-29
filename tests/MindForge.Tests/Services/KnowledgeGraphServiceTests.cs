using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class KnowledgeGraphServiceTests
{
    [Fact]
    public async Task AddNodeAndEdge_RetrievesCorrectGraph()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase("TestDb_KnowledgeGraph_1")
            .Options;
        using var db = new MindForgeDbContext(options);
        var service = new KnowledgeGraphService(db);
        var subjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var node1 = await service.AddNodeAsync(userId, subjectId, "Machine Learning", "Intro to ML");
        var node2 = await service.AddNodeAsync(userId, subjectId, "Neural Networks", "Intro to NN");
        await service.AddEdgeAsync(node1.Id, node2.Id, EdgeRelationType.Prerequisite);

        // Assert
        var graph = await service.GetGraphAsync(subjectId);
        Assert.Equal(2, graph.Count);
        
        var mlNode = graph.Find(n => n.Id == node1.Id);
        Assert.NotNull(mlNode);
        Assert.Single(mlNode.OutgoingEdges);
        Assert.Equal(node2.Id, mlNode.OutgoingEdges.First().ToNodeId);
        Assert.Equal(EdgeRelationType.Prerequisite, mlNode.OutgoingEdges.First().RelationType);
    }
}
