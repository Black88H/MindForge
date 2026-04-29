using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public interface IAPIKeyService
{
    Task<string?> GetApiKeyAsync(string provider);
    Task SaveApiKeyAsync(string provider, string apiKey);
    Task<bool> HasApiKeyAsync(string provider);
}
