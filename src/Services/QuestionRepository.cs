using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class QuestionRepository : IQuestionService
{
    private readonly MindForgeDbContext _db;

    public QuestionRepository(MindForgeDbContext db) => _db = db;

    public async Task<IEnumerable<Question>> GetQuestionsAsync(Guid subjectId)
        => await _db.Questions
            .Where(q => q.SubjectId == subjectId)
            .OrderBy(q => q.CreatedAt)
            .ToListAsync();

    public async Task<Question?> GetNextQuestionAsync(Guid subjectId)
    {
        // Spaced Repetition: Fragen mit niedrigster Erfolgsrate zuerst
        var questions = await _db.Questions
            .Where(q => q.SubjectId == subjectId)
            .ToListAsync();

        return questions
            .OrderBy(q => q.SuccessRate)
            .ThenBy(q => q.TimesAnswered)
            .FirstOrDefault();
    }

    public async Task<Question?> GetQuestionByIdAsync(Guid id)
        => await _db.Questions.Include(q => q.Answers).FirstOrDefaultAsync(q => q.Id == id);

    public async Task SaveAnswerAsync(Answer answer)
    {
        _db.Answers.Add(answer);

        // Statistiken der Frage aktualisieren
        var question = await _db.Questions.FindAsync(answer.QuestionId);
        if (question != null)
        {
            question.TimesAnswered++;
            if (answer.IsCorrect) question.TimesCorrect++;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<Answer>> GetAnswerHistoryAsync(Guid questionId)
        => await _db.Answers
            .Where(a => a.QuestionId == questionId)
            .OrderByDescending(a => a.Timestamp)
            .ToListAsync();

    public async Task AddQuestionAsync(Question question)
    {
        _db.Questions.Add(question);
        await _db.SaveChangesAsync();

        // Fragenanzahl im Subject aktualisieren
        await UpdateSubjectQuestionCountAsync(question.SubjectId);
    }

    public async Task UpdateQuestionAsync(Question question)
    {
        var existing = await _db.Questions.FindAsync(question.Id);
        if (existing != null)
            _db.Entry(existing).CurrentValues.SetValues(question);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteQuestionAsync(Guid id)
    {
        var question = await _db.Questions.FindAsync(id);
        if (question != null)
        {
            var subjectId = question.SubjectId;
            _db.Questions.Remove(question);
            await _db.SaveChangesAsync();
            await UpdateSubjectQuestionCountAsync(subjectId);
        }
    }

    public async Task<IEnumerable<Question>> SearchQuestionsAsync(string term)
        => await _db.Questions
            .Where(q => q.Text.Contains(term) || q.Explanation.Contains(term))
            .Take(50)
            .ToListAsync();

    public async Task<IEnumerable<Question>> GetWeakQuestionsAsync(Guid subjectId, int count = 20)
    {
        var questions = await _db.Questions
            .Where(q => q.SubjectId == subjectId && q.TimesAnswered > 0)
            .ToListAsync();

        return questions
            .Where(q => q.SuccessRate < 0.7)
            .OrderBy(q => q.SuccessRate)
            .Take(count);
    }

    private async Task UpdateSubjectQuestionCountAsync(Guid subjectId)
    {
        var subject = await _db.Subjects.FindAsync(subjectId);
        if (subject != null)
        {
            subject.QuestionCount = await _db.Questions.CountAsync(q => q.SubjectId == subjectId);

            var answers = await _db.Questions
                .Where(q => q.SubjectId == subjectId)
                .ToListAsync();

            subject.SuccessRate = answers.Count > 0
                ? answers.Average(q => q.SuccessRate)
                : 0;

            await _db.SaveChangesAsync();
        }
    }
}
