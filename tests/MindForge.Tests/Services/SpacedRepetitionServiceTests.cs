using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class SpacedRepetitionServiceTests
{
    [Fact]
    public async Task ProcessReviewAsync_Quality5_IncreasesInterval()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase("TestDb_SR_1")
            .Options;
        using var db = new MindForgeDbContext(options);
        var service = new SpacedRepetitionService(db);

        var item = new SpacedRepetitionItem 
        { 
            Id = Guid.NewGuid(), 
            UserId = Guid.NewGuid(), 
            KnowledgeNodeId = Guid.NewGuid(),
            EaseFactor = 2.5,
            Interval = 1,
            Repetitions = 1
        };
        db.SpacedRepetitionItems.Add(item);
        await db.SaveChangesAsync();

        // Act - Quality 5 (Perfect)
        await service.ProcessReviewAsync(item.Id, 5);

        // Assert
        var updatedItem = await db.SpacedRepetitionItems.FindAsync(item.Id);
        Assert.NotNull(updatedItem);
        Assert.Equal(6, updatedItem.Interval);
        Assert.Equal(2, updatedItem.Repetitions);
        Assert.Equal(2.6, Math.Round(updatedItem.EaseFactor, 1));
    }

    [Fact]
    public async Task ProcessReviewAsync_Quality0_ResetsRepetitions()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase("TestDb_SR_2")
            .Options;
        using var db = new MindForgeDbContext(options);
        var service = new SpacedRepetitionService(db);

        var item = new SpacedRepetitionItem 
        { 
            Id = Guid.NewGuid(), 
            UserId = Guid.NewGuid(), 
            KnowledgeNodeId = Guid.NewGuid(),
            EaseFactor = 2.5,
            Interval = 10,
            Repetitions = 4
        };
        db.SpacedRepetitionItems.Add(item);
        await db.SaveChangesAsync();

        // Act - Quality 0 (Complete Blackout)
        await service.ProcessReviewAsync(item.Id, 0);

        // Assert
        var updatedItem = await db.SpacedRepetitionItems.FindAsync(item.Id);
        Assert.NotNull(updatedItem);
        Assert.Equal(1, updatedItem.Interval);
        Assert.Equal(0, updatedItem.Repetitions);
        Assert.True(updatedItem.EaseFactor < 2.5); // Ease factor should decrease
    }
}
