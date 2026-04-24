using MindForge.Models;

namespace MindForge.Services;

public interface IContentGeneratorService
{
    Task<string> ExtractTextFromPdfAsync(string filePath);
    Task<IEnumerable<Question>> GenerateQuestionsAsync(string text, int count, DifficultyLevel difficulty);
    Task<string> GenerateSummaryAsync(string text);
    Task<string> GenerateCustomAsync(string text, string prompt);
    Task<string> ExportToMarkdownAsync(IEnumerable<Question> questions);
    Task<string> ExportToJsonAsync(IEnumerable<Question> questions);
}
