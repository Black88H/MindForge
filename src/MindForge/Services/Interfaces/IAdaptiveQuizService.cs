using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public record QuizQuestion(
    string Question,
    List<string> Options,
    int CorrectIndex,
    string Explanation,
    string Difficulty);

public record QuizResult(
    int TotalQuestions,
    int CorrectAnswers,
    double ScorePercent,
    string Feedback,
    int XPEarned);

public interface IAdaptiveQuizService
{
    Task<List<QuizQuestion>> GenerateQuizAsync(Guid notebookId, string difficulty = "Medium", int count = 10);
    Task<QuizResult> EvaluateAnswersAsync(Guid userId, Guid notebookId, List<QuizQuestion> questions, List<int> userAnswers);
}
