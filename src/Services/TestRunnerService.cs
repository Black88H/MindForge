using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class TestRunnerService
{
    private readonly MindForgeDbContext _db;
    private readonly GamificationService _gamification;

    public TestRunnerService(MindForgeDbContext db, GamificationService gamification)
    {
        _db = db;
        _gamification = gamification;
    }

    public async Task<TestSession> StartTestAsync(Guid testId, string userId, TestMode mode)
    {
        var test = await _db.Tests
            .Include(t => t.Subject)
            .FirstOrDefaultAsync(t => t.Id == testId)
            ?? throw new InvalidOperationException("Test nicht gefunden");

        var questions = await _db.Questions
            .Where(q => q.SubjectId == test.SubjectId)
            .Take(test.QuestionIds.Length > 0 ? test.QuestionIds.Length : 20)
            .ToListAsync();

        return new TestSession
        {
            TestId = testId,
            UserId = userId,
            Mode = mode,
            Questions = questions,
            StartTime = DateTime.UtcNow,
            TimeLimitMinutes = test.DurationMinutes > 0 ? test.DurationMinutes : 30
        };
    }

    public AnswerResult SubmitAnswer(TestSession session, Guid questionId, string answer)
    {
        var question = session.Questions.FirstOrDefault(q => q.Id == questionId);
        if (question == null) return new AnswerResult { IsCorrect = false };

        bool isCorrect = string.Equals(answer.Trim(), question.CorrectAnswer.Trim(),
            StringComparison.OrdinalIgnoreCase);

        session.Answers[questionId] = answer;
        if (isCorrect) session.CorrectCount++;

        return new AnswerResult
        {
            IsCorrect = isCorrect,
            CorrectAnswer = question.CorrectAnswer,
            Explanation = question.Explanation
        };
    }

    public async Task<TestSessionResult> EndTestAsync(TestSession session)
    {
        int total = session.Questions.Count;
        double score = total > 0 ? (double)session.CorrectCount / total * 100 : 0;
        var duration = DateTime.UtcNow - session.StartTime;

        var history = new UserTestHistory
        {
            UserId = session.UserId,
            TestId = session.TestId,
            Score = score,
            TotalQuestions = total,
            CorrectAnswers = session.CorrectCount,
            TimeTaken = duration,
            WrongAnswerIds = session.Questions
                .Where(q => session.Answers.TryGetValue(q.Id, out var a) &&
                            !string.Equals(a, q.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
                .Select(q => q.Id)
                .ToList()
        };
        _db.UserTestHistory.Add(history);
        await _db.SaveChangesAsync();

        var xpAction = score >= 100 ? XPAction.TestPerfect : XPAction.TestComplete;
        int customXp = (int)(score * 2);
        int xpEarned = await _gamification.AddXPAsync(session.UserId, xpAction, customXp);

        return new TestSessionResult
        {
            Score = score,
            CorrectAnswers = session.CorrectCount,
            TotalQuestions = total,
            TimeTaken = duration,
            XpEarned = xpEarned,
            WrongQuestions = session.Questions
                .Where(q => history.WrongAnswerIds.Contains(q.Id))
                .ToList()
        };
    }

    public double CalculateScore(int correct, int total) =>
        total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;

    public string GenerateErrorAnalysis(List<Question> wrongQuestions)
    {
        if (!wrongQuestions.Any()) return "Perfekt! Keine Fehler.";

        var byDifficulty = wrongQuestions
            .GroupBy(q => q.Difficulty)
            .Select(g => $"{g.Key}: {g.Count()}x")
            .ToList();

        return $"Häufige Fehler — {string.Join(", ", byDifficulty)}";
    }
}

public class TestSession
{
    public Guid TestId { get; set; }
    public string UserId { get; set; } = "default";
    public TestMode Mode { get; set; }
    public List<Question> Questions { get; set; } = [];
    public Dictionary<Guid, string> Answers { get; set; } = [];
    public int CorrectCount { get; set; }
    public DateTime StartTime { get; set; }
    public int TimeLimitMinutes { get; set; } = 30;
    public bool IsFinished { get; set; }
}

public class AnswerResult
{
    public bool IsCorrect { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public class TestSessionResult
{
    public double Score { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public TimeSpan TimeTaken { get; set; }
    public int XpEarned { get; set; }
    public List<Question> WrongQuestions { get; set; } = [];
    public string ErrorAnalysis { get; set; } = string.Empty;
}

public enum TestMode { Normal, Exam, Practice, Custom }
