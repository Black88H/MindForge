using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindForge.Models;
using MindForge.Services.AI.Interfaces;

namespace MindForge.Services;

public interface ITestService
{
    Task<Test> GenerateTestAsync(Guid subjectId, Difficulty difficulty, int coveragePercent, Guid userId);
    Task<Test> GenerateTestFromPhotoAsync(string photoPath, Guid subjectId, Guid userId);
    Task<bool> SubmitAnswerAsync(Guid questionId, string answer);
    Task<TestResultDto> CompleteTestAsync(Guid testId);
    Task SkipTestAsync(Guid testId);
    Task<FeynmanSession> StartFeynmanSessionAsync(Guid testId, Guid knowledgeNodeId, string userExplanation);
}

public record TestResultDto(float Score, int Correct, int Total, List<string> WeakTopics, List<string> Recommendations);

public class TestService : ITestService
{
    private readonly MindForgeDbContext _db;
    private readonly IAISelector _ai;
    private readonly IFileIngestionService _fileService;
    private readonly IGamificationService _gamification;
    private readonly IKnowledgeGraphService _knowledgeGraph;
    private readonly ILogger<TestService> _logger;

    public TestService(
        MindForgeDbContext db, IAISelector ai, IFileIngestionService fileService,
        IGamificationService gamification, IKnowledgeGraphService knowledgeGraph,
        ILogger<TestService> logger)
    {
        _db = db;
        _ai = ai;
        _fileService = fileService;
        _gamification = gamification;
        _knowledgeGraph = knowledgeGraph;
        _logger = logger;
    }

    public async Task<Test> GenerateTestAsync(Guid subjectId, Difficulty difficulty, int coveragePercent, Guid userId)
    {
        var materials = await _db.Materials
            .Where(m => m.SubjectId == subjectId && m.UserId == userId)
            .ToListAsync();

        if (!materials.Any())
            throw new InvalidOperationException("Keine Materialien im Fach vorhanden");

        var combinedContent = string.Join("\n\n", materials.Select(m => m.KiContent));

        // Token-Limit beachten — kürzen wenn nötig
        if (combinedContent.Length > 10000)
            combinedContent = combinedContent[..10000] + "\n[... gekürzt]";

        var questionCount = difficulty switch
        {
            Difficulty.Easy => 5,
            Difficulty.Medium => 10,
            Difficulty.Hard => 15,
            Difficulty.Exam => 20,
            _ => 10
        };

        var prompt = $@"Erstelle einen Test mit {questionCount} Fragen basierend auf diesem Lernmaterial.
Schwierigkeit: {difficulty}
Abdeckung: {coveragePercent}% des Materials sollen abgefragt werden.

Mische diese Fragetypen: MultipleChoice, FillBlank, FreeText, Matching, TrueFalse

Antworte NUR mit einem JSON-Array. Jedes Element:
{{
  ""questionType"": ""MultipleChoice"" | ""FillBlank"" | ""FreeText"" | ""Matching"" | ""TrueFalse"",
  ""questionText"": ""Die Frage"",
  ""options"": [""A"", ""B"", ""C"", ""D""] oder null,
  ""correctAnswer"": ""Die richtige Antwort""
}}

Material:
{combinedContent}

NUR valides JSON, kein Text davor/danach.";

        var response = await _ai.ExecuteAsync(AI.Models.TaskType.ContentGeneration, prompt);

        var test = new Test
        {
            UserId = userId,
            SubjectId = subjectId,
            Title = $"Test — {DateTime.Now:dd.MM.yyyy HH:mm}",
            TestSourceType = TestType.Generated,
            Difficulty = difficulty,
            CoveragePercent = coveragePercent
        };
        _db.Set<Test>().Add(test);

        try
        {
            var questions = JsonSerializer.Deserialize<List<QuestionDto>>(response.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                _db.TestQuestions.Add(new TestQuestion
                {
                    TestId = test.Id,
                    QuestionType = Enum.TryParse<QuestionType>(q.QuestionType, true, out var qt) ? qt : QuestionType.FreeText,
                    QuestionText = q.QuestionText ?? "",
                    Options = q.Options != null ? JsonSerializer.Serialize(q.Options) : null,
                    CorrectAnswer = q.CorrectAnswer ?? "",
                    OrderIndex = i
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Test-Generierung: JSON-Parse fehlgeschlagen");
            throw new InvalidOperationException("KI-Antwort konnte nicht verarbeitet werden");
        }

        await _db.SaveChangesAsync();
        return test;
    }

    public async Task<Test> GenerateTestFromPhotoAsync(string photoPath, Guid subjectId, Guid userId)
    {
        // OCR auf Foto
        var material = await _fileService.IngestFileAsync(photoPath, subjectId, userId);

        var prompt = $@"Analysiere diesen hochgeladenen Test und identifiziere die Fragetypen.
Erstelle dann einen ähnlichen Test mit neuen Fragen aus dem gleichen Themengebiet.

Originaler Test (per OCR extrahiert):
{material.KiContent}

Erstelle ähnliche Fragen im gleichen Stil. Antworte NUR mit JSON-Array wie:
[{{""questionType"":""..."",""questionText"":""..."",""options"":[...],""correctAnswer"":""...""}}]";

        var response = await _ai.ExecuteAsync(AI.Models.TaskType.ContentGeneration, prompt);

        var test = new Test
        {
            UserId = userId,
            SubjectId = subjectId,
            Title = $"Test (Foto) — {DateTime.Now:dd.MM.yyyy HH:mm}",
            TestSourceType = TestType.FromPhoto,
            SourcePhotoPath = photoPath
        };
        _db.Set<Test>().Add(test);

        try
        {
            var questions = JsonSerializer.Deserialize<List<QuestionDto>>(response.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                _db.TestQuestions.Add(new TestQuestion
                {
                    TestId = test.Id,
                    QuestionType = Enum.TryParse<QuestionType>(q.QuestionType, true, out var qt) ? qt : QuestionType.FreeText,
                    QuestionText = q.QuestionText ?? "",
                    Options = q.Options != null ? JsonSerializer.Serialize(q.Options) : null,
                    CorrectAnswer = q.CorrectAnswer ?? "",
                    OrderIndex = i
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Foto-Test: JSON-Parse fehlgeschlagen");
        }

        await _db.SaveChangesAsync();
        return test;
    }

    public async Task<bool> SubmitAnswerAsync(Guid questionId, string answer)
    {
        var question = await _db.TestQuestions.FindAsync(questionId)
            ?? throw new ArgumentException("Frage nicht gefunden");

        question.UserAnswer = answer;

        if (question.QuestionType == QuestionType.FreeText)
        {
            // KI bewertet Freitextantwort
            var prompt = $@"Bewerte ob diese Antwort korrekt ist.
Frage: {question.QuestionText}
Korrekte Antwort: {question.CorrectAnswer}
Nutzer-Antwort: {answer}

Antworte NUR mit JSON: {{""isCorrect"": true/false, ""explanation"": ""kurze Erklärung""}}";

            var response = await _ai.ExecuteAsync(AI.Models.TaskType.QAExplanation, prompt);
            try
            {
                var result = JsonSerializer.Deserialize<AnswerEvalDto>(response.Content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                question.IsCorrect = result?.IsCorrect ?? false;
                question.Explanation = result?.Explanation;
            }
            catch
            {
                question.IsCorrect = answer.Trim().Equals(question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }
        else
        {
            question.IsCorrect = answer.Trim().Equals(question.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        await _db.SaveChangesAsync();
        return question.IsCorrect ?? false;
    }

    public async Task<TestResultDto> CompleteTestAsync(Guid testId)
    {
        var test = await _db.Set<Test>()
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.Id == testId)
            ?? throw new ArgumentException("Test nicht gefunden");

        var answered = test.Questions.Where(q => q.UserAnswer != null).ToList();
        var correct = answered.Count(q => q.IsCorrect == true);
        var total = test.Questions.Count;
        var score = total > 0 ? (float)correct / total : 0;

        test.Score = score;
        test.CompletedAt = DateTime.UtcNow;

        // XP vergeben
        if (score >= 0.7f)
            await _gamification.AwardXPAsync(test.UserId, 50, XPSource.TestCompleted, $"Test bestanden: {score:P0}");
        if (score >= 1.0f)
            await _gamification.AwardXPAsync(test.UserId, 100, XPSource.TestCompleted, "Perfekter Test!");

        await _db.SaveChangesAsync();

        // Schwächen identifizieren
        var weakTopics = answered
            .Where(q => q.IsCorrect == false)
            .Select(q => q.QuestionText)
            .Take(5)
            .ToList();

        var recommendations = weakTopics.Any()
            ? new List<string> { "Wiederhole die falsch beantworteten Themen", "Erstelle Karteikarten zu deinen Schwächen", "Nutze die Feynman-Methode" }
            : new List<string> { "Hervorragend! Versuche einen schwierigeren Test." };

        return new TestResultDto(score, correct, total, weakTopics, recommendations);
    }

    public async Task SkipTestAsync(Guid testId)
    {
        var test = await _db.Set<Test>().FindAsync(testId);
        if (test != null)
        {
            test.IsSkipped = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<FeynmanSession> StartFeynmanSessionAsync(Guid testId, Guid knowledgeNodeId, string userExplanation)
    {
        var node = await _db.KnowledgeNodes.FindAsync(knowledgeNodeId)
            ?? throw new ArgumentException("Wissensknoten nicht gefunden");

        var prompt = $@"Ein Schüler versucht das Konzept ""{node.Title}"" zu erklären wie einem 5-Jährigen.
Hier ist die Erklärung des Schülers:

""{userExplanation}""

Bewerte die Erklärung. Antworte NUR mit JSON:
{{
  ""masteryScore"": 0.0-1.0,
  ""assessment"": ""Deine Bewertung der Erklärung"",
  ""gaps"": [""Lücke 1"", ""Lücke 2""]
}}";

        var response = await _ai.ExecuteAsync(AI.Models.TaskType.QAExplanation, prompt);

        var session = new FeynmanSession
        {
            TestId = testId,
            KnowledgeNodeId = knowledgeNodeId,
            UserExplanation = userExplanation
        };

        try
        {
            var result = JsonSerializer.Deserialize<FeynmanResultDto>(response.Content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            session.MasteryScore = result?.MasteryScore ?? 0;
            session.AiAssessment = result?.Assessment ?? response.Content;
            session.GapsIdentified = result?.Gaps != null ? JsonSerializer.Serialize(result.Gaps) : "[]";
        }
        catch
        {
            session.AiAssessment = response.Content;
            session.GapsIdentified = "[]";
            session.MasteryScore = 0.5f;
        }

        // Mastery aktualisieren
        await _knowledgeGraph.UpdateMasteryAsync(knowledgeNodeId, session.MasteryScore);

        if (session.MasteryScore >= 0.7f)
            await _gamification.AwardXPAsync(
                (await _db.Set<Test>().FindAsync(testId))?.UserId ?? Guid.Empty,
                75, XPSource.FeynmanPassed, $"Feynman: {node.Title}");

        _db.FeynmanSessions.Add(session);
        await _db.SaveChangesAsync();

        return session;
    }

    private record QuestionDto(string? QuestionType, string? QuestionText, List<string>? Options, string? CorrectAnswer);
    private record AnswerEvalDto(bool IsCorrect, string? Explanation);
    private record FeynmanResultDto(float MasteryScore, string? Assessment, List<string>? Gaps);
}
