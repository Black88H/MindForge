using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class AuthServiceTests
{
    [Fact]
    public async Task RegisterAsync_ValidData_CreatesUser()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase("TestDb_Auth_1")
            .Options;
        using var db = new MindForgeDbContext(options);
        var authService = new AuthService(db);
        
        // Act
        var result = await authService.RegisterAsync("Test", "test@test.com", "password123");
        
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Test", result.Data.Username);
        Assert.Equal("test@test.com", result.Data.Email);
    }
}
