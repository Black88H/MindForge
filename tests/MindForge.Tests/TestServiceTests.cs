using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MindForge.Tests;

public class TestServiceTests : IDisposable
{
    private readonly MindForgeDbContext       _db;
    private readonly Mock<IAISelector>        _aiMock;
    private readonly Mock<IGamificationService> _gamMock;
    private readonly Mock<IKnowledgeGraphService> _graphMock;
    private readonly Mock<IFileIngestionService>  _fileMock;
    private readonly TestService              _sut;
    private readonly Guid                    _userId    = Guid.NewGuid();
    private readonly Guid                    _subjectId = Guid.NewGuid();

    public TestServiceTests()
    {
        var opts = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db        = new MindForgeDbContext(opts);
        _aiMock    = new Mock<IAISelector>();
        _gamMock   = new Mock<IGamificationService>();
        _graphMock = new Mock<IKnowledgeGraphService>();
        _fileMock  = new Mock<IFileIngestionService>();

        // Default AI response (valid JSON test questions)
        _aiMock.Setup(ai => ai.ExecuteAsync(It.IsAny<TaskType>(), It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync(new AIResponse
               {
                   IsSuccess = true,
                   Content   = """[{"questionType":"TrueFalse","questionText":"Ist 1+1=2?","options":null,"correctAnswer":"True"}]""",
                   ProviderName = "MockAI"
               });

        // Default gamification: returns a dummy XPEvent
        _gamMock.Setup(g => g.AwardXPAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<XPSource>(), It.IsAny<string>()))
                .ReturnsAsync(new XPEvent { UserId = _userId, Amount = 50, Source = XPSource.TestCompleted });

        _sut = new TestService(_db, _aiMock.Object, _fileMock.Object, _gamMock.Object,
                               _graphMock.Object, NullLogger<TestService>.Instance);

        // Seed user + subject
        _db.Users.Add(new User { Id = _userId, Username = "tester", Email = "t@t.de" });
        _db.Subjects.Add(new Subject { Id = _subjectId, Name = "Mathe", Icon = "∫", Color = "#fff" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── SubmitAnswerAsync – non-FreeText (string comparison) ─────────────

    [Fact]
    public async Task SubmitAnswer_CorrectAnswer_MarksCorrect()
    {
        var (test, question) = await CreateTestWithOneQuestion("Paris", QuestionType.MultipleChoice);

        var correct = await _sut.SubmitAnswerAsync(question.Id, "Paris");

        correct.Should().BeTrue();
        var updated = await _db.TestQuestions.FindAsync(question.Id);
        updated!.IsCorrect.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_CorrectAnswer_IsCaseInsensitive()
    {
        var (_, question) = await CreateTestWithOneQuestion("paris", QuestionType.TrueFalse);

        var correct = await _sut.SubmitAnswerAsync(question.Id, "PARIS");

        correct.Should().BeTrue();
    }

    [Fact]
    public async Task SubmitAnswer_WrongAnswer_MarksIncorrect()
    {
        var (_, question) = await CreateTestWithOneQuestion("Paris", QuestionType.MultipleChoice);

        var correct = await _sut.SubmitAnswerAsync(question.Id, "Berlin");

        correct.Should().BeFalse();
        var updated = await _db.TestQuestions.FindAsync(question.Id);
        updated!.IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitAnswer_SavesUserAnswer()
    {
        var (_, question) = await CreateTestWithOneQuestion("Paris", QuestionType.MultipleChoice);

        await _sut.SubmitAnswerAsync(question.Id, "London");

        var updated = await _db.TestQuestions.FindAsync(question.Id);
        updated!.UserAnswer.Should().Be("London");
    }

    [Fact]
    public async Task SubmitAnswer_FreeText_CallsAI()
    {
        _aiMock.Setup(ai => ai.ExecuteAsync(It.IsAny<TaskType>(), It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync(new AIResponse
               {
                   IsSuccess    = true,
                   Content      = """{"isCorrect":true,"explanation":"Gut erklärt!"}""",
                   ProviderName = "MockAI"
               });

        var (_, question) = await CreateTestWithOneQuestion("42", QuestionType.FreeText);

        var correct = await _sut.SubmitAnswerAsync(question.Id, "zweiundvierzig");

        correct.Should().BeTrue();
        _aiMock.Verify(ai => ai.ExecuteAsync(It.IsAny<TaskType>(), It.Is<string>(p => p.Contains("zweiundvierzig")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SubmitAnswer_NonExistentQuestion_ThrowsArgumentException()
    {
        var act = async () => await _sut.SubmitAnswerAsync(Guid.NewGuid(), "x");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── CompleteTestAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task CompleteTest_CalculatesScore_AllCorrect()
    {
        var test = await CreateTestWithAnsweredQuestions(correctCount: 3, wrongCount: 0);

        var result = await _sut.CompleteTestAsync(test.Id);

        result.Score.Should().BeApproximately(1.0f, 0.001f);
        result.Correct.Should().Be(3);
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task CompleteTest_CalculatesScore_Mixed()
    {
        var test = await CreateTestWithAnsweredQuestions(correctCount: 7, wrongCount: 3);

        var result = await _sut.CompleteTestAsync(test.Id);

        result.Score.Should().BeApproximately(0.7f, 0.001f);
        result.Correct.Should().Be(7);
        result.Total.Should().Be(10);
    }

    [Fact]
    public async Task CompleteTest_CalculatesScore_AllWrong()
    {
        var test = await CreateTestWithAnsweredQuestions(correctCount: 0, wrongCount: 5);

        var result = await _sut.CompleteTestAsync(test.Id);

        result.Score.Should().BeApproximately(0.0f, 0.001f);
        result.Correct.Should().Be(0);
    }

    [Fact]
    public async Task CompleteTest_AwardsXP_WhenScoreAtLeast70Percent()
    {
        var test = await CreateTestWithAnsweredQuestions(correctCount: 7, wrongCount: 3);

        await _sut.CompleteTestAsync(test.Id);

        _gamMock.Verify(g => g.AwardXPAsync(_userId, 50, XPSource.TestCompleted, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CompleteTest_AwardsBonusXP_OnPerfectScore()
    {
        var test = await CreateTestWithAnsweredQuestions(correctCount: 5, wrongCount: 0);

        await _sut.CompleteTestAsync(test.Id);

        // Should award both the 50 XP (≥70%) and the 100 XP bonus (100%)
        _gamMock.Verify(g => g.AwardXPAsync(_userId, It.IsInRange(50, 100, Moq.Range.Inclusive), XPSource.TestCompleted, It.IsAny<string>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task CompleteTest_NoXP_WhenScoreBelow70Percent()
    {
        var test = await CreateTestWithAnsweredQuestions(correctCount: 3, wrongCount: 7);

        await _sut.CompleteTestAsync(test.Id);

        _gamMock.Verify(g => g.AwardXPAsync(It.IsAny<Guid>(), It.IsAny<int>(), XPSource.TestCompleted, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CompleteTest_SetsCompletedAt()
    {
        var test = await CreateTestWithAnsweredQuestions(correctCount: 3, wrongCount: 0);
        var before = DateTime.UtcNow;

        await _sut.CompleteTestAsync(test.Id);

        var updated = await _db.Tests.FindAsync(test.Id);
        updated!.CompletedAt.Should().NotBeNull();
        updated.CompletedAt.Should().BeAfter(before.AddSeconds(-1));
    }

    [Fact]
    public async Task CompleteTest_IdentifiesWeakTopics()
    {
        var test = await CreateTestWithAnsweredQuestions(correctCount: 0, wrongCount: 3);

        var result = await _sut.CompleteTestAsync(test.Id);

        result.WeakTopics.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CompleteTest_NonExistentTest_ThrowsArgumentException()
    {
        var act = async () => await _sut.CompleteTestAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── SkipTestAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SkipTest_MarksTestAsSkipped()
    {
        var test = new Test { UserId = _userId, SubjectId = _subjectId, Title = "Skip-Test" };
        _db.Tests.Add(test);
        await _db.SaveChangesAsync();

        await _sut.SkipTestAsync(test.Id);

        var updated = await _db.Tests.FindAsync(test.Id);
        updated!.IsSkipped.Should().BeTrue();
    }

    [Fact]
    public async Task SkipTest_NonExistentTest_DoesNotThrow()
    {
        var act = async () => await _sut.SkipTestAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    // ── GenerateTestAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateTest_NoMaterials_ThrowsInvalidOperationException()
    {
        var act = async () => await _sut.GenerateTestAsync(_subjectId, Difficulty.Easy, 50, _userId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateTest_WithMaterials_CreatesTestInDb()
    {
        _db.Materials.Add(new Material
        {
            SubjectId = _subjectId,
            UserId    = _userId,
            OriginalFileName = "test.pdf",
            OriginalFilePath = "/tmp/test.pdf",
            KiContent        = "Inhalt für den Test",
            KiContentHash    = "abc123"
        });
        await _db.SaveChangesAsync();

        var test = await _sut.GenerateTestAsync(_subjectId, Difficulty.Easy, 50, _userId);

        test.Should().NotBeNull();
        test.UserId.Should().Be(_userId);
        test.SubjectId.Should().Be(_subjectId);
        _db.Tests.Any(t => t.Id == test.Id).Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<(Test test, TestQuestion question)> CreateTestWithOneQuestion(
        string correctAnswer, QuestionType type)
    {
        var test = new Test { UserId = _userId, SubjectId = _subjectId, Title = "UnitTest" };
        var question = new TestQuestion
        {
            TestId       = test.Id,
            QuestionText = "Frage",
            CorrectAnswer = correctAnswer,
            QuestionType = type
        };
        test.Questions.Add(question);
        _db.Tests.Add(test);
        await _db.SaveChangesAsync();
        return (test, question);
    }

    private async Task<Test> CreateTestWithAnsweredQuestions(int correctCount, int wrongCount)
    {
        var test = new Test { UserId = _userId, SubjectId = _subjectId, Title = "ScoreTest" };
        _db.Tests.Add(test);

        for (int i = 0; i < correctCount; i++)
        {
            _db.TestQuestions.Add(new TestQuestion
            {
                TestId        = test.Id,
                QuestionText  = $"Richtig-Frage {i}",
                CorrectAnswer = "Korrekt",
                QuestionType  = QuestionType.TrueFalse,
                UserAnswer    = "Korrekt",
                IsCorrect     = true
            });
        }

        for (int i = 0; i < wrongCount; i++)
        {
            _db.TestQuestions.Add(new TestQuestion
            {
                TestId        = test.Id,
                QuestionText  = $"Falsch-Frage {i}",
                CorrectAnswer = "Korrekt",
                QuestionType  = QuestionType.TrueFalse,
                UserAnswer    = "Falsch",
                IsCorrect     = false
            });
        }

        await _db.SaveChangesAsync();
        return test;
    }
}
