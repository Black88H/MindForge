using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class ChatServiceTests
{
    [Fact]
    public async Task SendMessageAsync_SavesMessagesAndReturnsResponse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase("TestDb_Chat_1")
            .Options;
        using var db = new MindForgeDbContext(options);
        var service = new ChatService(db);
        var userId = Guid.NewGuid();
        var prompt = "Erkläre mir Quantenphysik.";

        // Act
        var response = await service.SendMessageAsync(userId, prompt);

        // Assert
        Assert.NotNull(response);
        
        var messages = await db.ChatMessages.ToListAsync();
        Assert.Equal(2, messages.Count);
        
        var userMsg = messages.Find(m => m.Role == ChatRole.User);
        Assert.NotNull(userMsg);
        Assert.Equal(prompt, userMsg.Content);

        var botMsg = messages.Find(m => m.Role == ChatRole.Assistant);
        Assert.NotNull(botMsg);
        Assert.Equal(response, botMsg.Content);
    }
}
