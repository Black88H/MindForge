namespace MindForge.Services;

public interface IAIProvider
{
    string ProviderName { get; }
    bool IsConfigured { get; }
    Task<string> ExplainQuestionAsync(string question, string correctAnswer);
    Task<string> GenerateQuestionsFromTextAsync(string text, int count);
    Task<string> SummarizeAsync(string text);
    Task<string> ChatAsync(string prompt);
}
