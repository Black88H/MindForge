using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class TestRepository : ITestService
{
    private readonly MindForgeDbContext _db;

    public TestRepository(MindForgeDbContext db) => _db = db;

    public async Task<Test> CreateTestAsync(
        string name, Guid[] questionIds, int durationMinutes,
        DifficultyLevel difficulty, TestType type)
    {
        var test = new Test
        {
            Name = name,
            QuestionIds = questionIds,
            DurationMinutes = durationMinutes,
            Difficulty = difficulty,
            Type = type,
        };
        _db.Tests.Add(test);
        await _db.SaveChangesAsync();
        return test;
    }

    public async Task<Test?> GetTestByIdAsync(Guid id)
        => await _db.Tests.Include(t => t.Results).FirstOrDefaultAsync(t => t.Id == id);

    public async Task<IEnumerable<Test>> GetTestsAsync()
        => await _db.Tests.OrderByDescending(t => t.CreatedAt).ToListAsync();

    public async Task<TestResult> SubmitTestAsync(Guid testId, Answer[] answers)
    {
        var correct = answers.Count(a => a.IsCorrect);
        var score = answers.Length > 0 ? (double)correct / answers.Length * 100 : 0;
        var xp = (int)(score / 100 * 30 + (score >= 80 ? 20 : 0));

        var result = new TestResult
        {
            TestId = testId,
            Score = score,
            CorrectCount = correct,
            TotalCount = answers.Length,
            XpEarned = xp,
        };

        _db.TestResults.Add(result);

        // Antworten speichern
        foreach (var a in answers) _db.Answers.Add(a);

        await _db.SaveChangesAsync();
        return result;
    }

    public async Task<IEnumerable<TestResult>> GetResultsAsync(Guid testId)
        => await _db.TestResults
            .Where(r => r.TestId == testId)
            .OrderByDescending(r => r.CompletedAt)
            .ToListAsync();

    public async Task DeleteTestAsync(Guid id)
    {
        var test = await _db.Tests.FindAsync(id);
        if (test != null)
        {
            _db.Tests.Remove(test);
            await _db.SaveChangesAsync();
        }
    }
}
