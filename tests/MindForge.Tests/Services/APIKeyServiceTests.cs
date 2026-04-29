using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class APIKeyServiceTests
{
    [Fact]
    public async Task SaveAndGetApiKey_EncryptsAndDecryptsCorrectly()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase("TestDb_ApiKey_1")
            .Options;
        using var db = new MindForgeDbContext(options);
        var service = new APIKeyService(db);
        var testProvider = "OpenAI";
        var testKey = "sk-test-12345";

        // Act
        await service.SaveApiKeyAsync(testProvider, testKey);
        var retrievedKey = await service.GetApiKeyAsync(testProvider);
        var hasKey = await service.HasApiKeyAsync(testProvider);

        // Assert
        Assert.True(hasKey);
        Assert.Equal(testKey, retrievedKey);

        // Verify it is encrypted in DB
        var setting = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == $"APIKey_{testProvider.ToUpperInvariant()}");
        Assert.NotNull(setting);
        Assert.NotEqual(testKey, setting.Value); // Should be encrypted
    }
}
