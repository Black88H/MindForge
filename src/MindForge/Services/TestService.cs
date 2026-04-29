using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class TestService : ITestService
{
    private readonly MindForgeDbContext _db;
    private readonly IGamificationService _gamification;

    public TestService(MindForgeDbContext db, IGamificationService gamification)
    {
        _db = db;
        _gamification = gamification;
    }

    public async Task<Test> CreateTestAsync(Guid userId, Guid subjectId, string title, List<TestQuestion> questions)
    {
        var test = new Test
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubjectId = subjectId,
            Title = title,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tests.Add(test);
        await _db.SaveChangesAsync();

        // Attach questions to this test
        foreach (var (q, i) in questions.Select((q, i) => (q, i)))
        {
            q.Id = Guid.NewGuid();
            q.TestId = test.Id;
            q.OrderIndex = i;
        }
        _db.TestQuestions.AddRange(questions);
        await _db.SaveChangesAsync();

        test.Questions = questions;
        return test;
    }

    public async Task<Test?> GetTestAsync(Guid testId)
    {
        return await _db.Tests
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == testId);
    }

    public async Task<Test> SubmitTestAsync(Guid testId, Dictionary<Guid, string> answers)
    {
        var test = await _db.Tests
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == testId)
            ?? throw new InvalidOperationException($"Test {testId} nicht gefunden.");

        int correct = 0;
        int total = test.Questions.Count;

        foreach (var question in test.Questions)
        {
            if (answers.TryGetValue(question.Id, out var userAnswer))
            {
                question.UserAnswer = userAnswer;
                question.IsCorrect = string.Equals(
                    userAnswer.Trim(),
                    question.CorrectAnswer.Trim(),
                    StringComparison.OrdinalIgnoreCase
                );
                if (question.IsCorrect == true) correct++;
            }
            else
            {
                question.UserAnswer = null;
                question.IsCorrect = false;
            }
        }

        test.Score = total > 0 ? Math.Round((double)correct / total * 100, 1) : 0;
        test.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Award XP based on score
        int xp = (int)(test.Score.Value / 10) * 10; // 10 XP per 10% score
        if (xp > 0)
        {
            await _gamification.AwardXPAsync(test.UserId, xp, XPSource.TestCompleted, $"Test abgeschlossen: {test.Title} ({test.Score}%)");
        }

        return test;
    }

    public async Task<List<Test>> GetTestHistoryAsync(Guid userId)
    {
        return await _db.Tests
            .Include(t => t.Questions)
            .Where(t => t.UserId == userId && t.CompletedAt != null)
            .OrderByDescending(t => t.CompletedAt)
            .ToListAsync();
    }
}
