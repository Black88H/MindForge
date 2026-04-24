using MindForge.Models;

namespace MindForge.Services;

public interface IQuestionService
{
    Task<IEnumerable<Question>> GetQuestionsAsync(Guid subjectId);
    Task<Question?> GetNextQuestionAsync(Guid subjectId);
    Task<Question?> GetQuestionByIdAsync(Guid id);
    Task SaveAnswerAsync(Answer answer);
    Task<IEnumerable<Answer>> GetAnswerHistoryAsync(Guid questionId);
    Task AddQuestionAsync(Question question);
    Task UpdateQuestionAsync(Question question);
    Task DeleteQuestionAsync(Guid id);
}
