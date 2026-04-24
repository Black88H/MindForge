using MindForge.Models;

namespace MindForge.Services;

public interface ITestService
{
    Task<Test> CreateTestAsync(string name, Guid[] questionIds, int durationMinutes, DifficultyLevel difficulty, TestType type);
    Task<Test?> GetTestByIdAsync(Guid id);
    Task<IEnumerable<Test>> GetTestsAsync();
    Task<TestResult> SubmitTestAsync(Guid testId, Answer[] answers);
    Task<IEnumerable<TestResult>> GetResultsAsync(Guid testId);
}
