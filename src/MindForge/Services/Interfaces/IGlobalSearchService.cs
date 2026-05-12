using System.Collections.Generic;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public record SearchResult(
    string EntityType,
    System.Guid EntityId,
    string Title,
    string Snippet,
    string Icon);

public interface IGlobalSearchService
{
    Task<IReadOnlyList<SearchResult>> SearchAsync(string query);
    Task RebuildIndexAsync();
}
