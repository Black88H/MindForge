using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public interface ITestService
{
    Task<Test> CreateTestAsync(Guid userId, Guid subjectId, string title, List<TestQuestion> questions);
    Task<Test?> GetTestAsync(Guid testId);
    Task<Test> SubmitTestAsync(Guid testId, Dictionary<Guid, string> answers);
    Task<List<Test>> GetTestHistoryAsync(Guid userId);
}
