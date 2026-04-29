using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services;
using Xunit;

namespace MindForge.Tests.Services;

public class TestServiceTests
{
    private static (MindForgeDbContext db, TestService service) CreateService(string dbName)
    {
        var options = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new MindForgeDbContext(options);
        var gamification = new GamificationService(db);
        var service = new TestService(db, gamification);
        return (db, service);
    }

    [Fact]
    public async Task CreateTestAsync_SavesTestWithQuestions()
    {
        var (db, service) = CreateService("TestDb_TestService_1");
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        // Arrange user for XP awarding
        db.Users.Add(new User { Id = userId, TotalXP = 0, Level = 1 });
        await db.SaveChangesAsync();

        var questions = new List<TestQuestion>
        {
            new TestQuestion { QuestionText = "Was ist 2+2?", CorrectAnswer = "4" },
            new TestQuestion { QuestionText = "Was ist die Hauptstadt von Deutschland?", CorrectAnswer = "Berlin" }
        };

        // Act
        var test = await service.CreateTestAsync(userId, subjectId, "Mathe Test", questions);

        // Assert
        Assert.NotEqual(Guid.Empty, test.Id);
        var savedTest = await db.Tests.Include(t => t.Questions).FirstAsync(t => t.Id == test.Id);
        Assert.Equal(2, savedTest.Questions.Count);
    }

    [Fact]
    public async Task SubmitTestAsync_CalculatesScoreCorrectly()
    {
        var (db, service) = CreateService("TestDb_TestService_2");
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        db.Users.Add(new User { Id = userId, TotalXP = 0, Level = 1 });
        await db.SaveChangesAsync();

        var questions = new List<TestQuestion>
        {
            new TestQuestion { QuestionText = "Was ist 2+2?", CorrectAnswer = "4" },
            new TestQuestion { QuestionText = "Hauptstadt Deutschlands?", CorrectAnswer = "Berlin" },
            new TestQuestion { QuestionText = "PI gerundet?", CorrectAnswer = "3" },
            new TestQuestion { QuestionText = "Farbe des Himmels?", CorrectAnswer = "Blau" }
        };

        var test = await service.CreateTestAsync(userId, subjectId, "Test", questions);
        var qList = test.Questions.ToList();

        // Answer 3 of 4 correctly
        var answers = new Dictionary<Guid, string>
        {
            { qList[0].Id, "4" },        // correct
            { qList[1].Id, "Berlin" },   // correct
            { qList[2].Id, "3" },        // correct
            { qList[3].Id, "Rot" }       // wrong
        };

        // Act
        var result = await service.SubmitTestAsync(test.Id, answers);

        // Assert
        Assert.Equal(75.0, result.Score);
        Assert.NotNull(result.CompletedAt);
        Assert.True(result.Questions.ElementAt(0).IsCorrect);
        Assert.False(result.Questions.ElementAt(3).IsCorrect);
    }
}
