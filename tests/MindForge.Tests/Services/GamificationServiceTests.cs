using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class GamificationServiceTests
{
    [Fact]
    public async Task AwardXPAsync_AddsXPAndLevelsUp()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase("TestDb_Gamification_1")
            .Options;
        using var db = new MindForgeDbContext(options);
        var service = new GamificationService(db);
        
        var user = new User { Id = Guid.NewGuid(), TotalXP = 900, Level = 1 };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        // Act
        await service.AwardXPAsync(user.Id, 200, XPSource.TestCompleted, "Test XP");
        
        // Assert
        var updatedUser = await db.Users.FindAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal(1100, updatedUser.TotalXP);
        Assert.Equal(2, updatedUser.Level); // Leveled up
    }
}
