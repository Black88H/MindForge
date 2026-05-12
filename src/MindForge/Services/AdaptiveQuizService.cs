using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.AI;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class AdaptiveQuizService : IAdaptiveQuizService
{
    private readonly MindForgeDbContext _db;
    private readonly AISelector         _ai;

    public AdaptiveQuizService(MindForgeDbContext db, AISelector ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<List<QuizQuestion>> GenerateQuizAsync(
        Guid notebookId, string difficulty = "Medium", int count = 10)
    {
        // Pull material content for context
        var chunks = await _db.MaterialChunks
            .Where(c => c.NotebookId == notebookId)
            .OrderBy(_ => Guid.NewGuid())          // random sample
            .Take(6)
            .ToListAsync();

        var context = string.Join("\n\n", chunks.Select(c => c.Text));
        if (string.IsNullOrWhiteSpace(context))
        {
            // Fall back to notebook name
            var nb = await _db.Notebooks.FindAsync(notebookId);
            context = nb?.Name ?? "General knowledge";
        }

        var prompt =
            $"Du bist ein Lern-Assistent. Erstelle {count} Multiple-Choice-Fragen auf Deutsch " +
            $"zum folgenden Lernstoff. Schwierigkeitslevel: {difficulty}.\n\n" +
            "LERNSTOFF:\n" +
            context[..Math.Min(context.Length, 3000)] + "\n\n" +
            "Antworte NUR mit einem JSON-Array in folgendem Format (kein Text davor oder danach):\n" +
            "[\n" +
            "  {\n" +
            "    \"question\": \"Frage?\",\n" +
            "    \"options\": [\"A\", \"B\", \"C\", \"D\"],\n" +
            "    \"correctIndex\": 0,\n" +
            "    \"explanation\": \"Erklarung\",\n" +
            $"    \"difficulty\": \"{difficulty}\"\n" +
            "  }\n" +
            "]";

        try
        {
            var (provider, model) = await _ai.SelectAsync(AITask.QnA);
            var json = await provider.GenerateAsync(model, prompt);

            // Extract JSON array from response
            var start = json.IndexOf('[');
            var end   = json.LastIndexOf(']');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];

            var questions = JsonSerializer.Deserialize<List<QuizQuestion>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return questions ?? GenerateFallbackQuiz(count, difficulty);
        }
        catch
        {
            return GenerateFallbackQuiz(count, difficulty);
        }
    }

    public async Task<QuizResult> EvaluateAnswersAsync(
        Guid userId, Guid notebookId,
        List<QuizQuestion> questions, List<int> userAnswers)
    {
        var correct = 0;
        for (int i = 0; i < Math.Min(questions.Count, userAnswers.Count); i++)
            if (userAnswers[i] == questions[i].CorrectIndex) correct++;

        var total   = questions.Count;
        var pct     = total > 0 ? (double)correct / total * 100 : 0;
        var xp      = (int)(pct / 100 * 50) + correct * 10;

        var feedback = pct switch
        {
            >= 90 => "Ausgezeichnet! 🏆 Du beherrschst dieses Thema sehr gut.",
            >= 70 => "Gut gemacht! 👍 Ein paar Lücken, aber solide Basis.",
            >= 50 => "Befriedigend. 📚 Wiederhole die schwierigen Themen nochmal.",
            _     => "Weiter üben! 💪 Das Thema braucht mehr Aufmerksamkeit."
        };

        // Persist daily stat
        await IncrementQuizStatAsync(userId, xp);

        return new QuizResult(total, correct, pct, feedback, xp);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<QuizQuestion> GenerateFallbackQuiz(int count, string difficulty)
    {
        var q = new QuizQuestion(
            "Was ist die Hauptfunktion eines Betriebssystems?",
            ["Textverarbeitung", "Hardware-Verwaltung", "Internetzugang", "Dateiarchivierung"],
            1,
            "Ein Betriebssystem verwaltet die Hardware-Ressourcen und stellt eine Schnittstelle für Anwendungsprogramme bereit.",
            difficulty);
        return Enumerable.Range(0, count).Select(_ => q).ToList();
    }

    private async Task IncrementQuizStatAsync(Guid userId, int xp)
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var stat  = await _db.StudyStatistics
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Date == today);
            if (stat is null)
            {
                stat = new StudyStatistic { Id = Guid.NewGuid(), UserId = userId, Date = today };
                _db.StudyStatistics.Add(stat);
            }
            stat.QuizzesTaken++;
            stat.XPEarned += xp;
            await _db.SaveChangesAsync();
        }
        catch { /* non-critical */ }
    }
}
