using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public record CitedAnswer(string Answer, List<Citation> Citations);
public record Citation(Guid MaterialId, string MaterialTitle, string Excerpt);

public interface INotebookService
{
    Task<string> SummarizeMaterialAsync(Guid materialId, CancellationToken ct = default);
    Task<string> SummarizeSubjectAsync(Guid subjectId, int maxMaterials = 5, CancellationToken ct = default);
    Task<CitedAnswer> AskWithSourcesAsync(Guid userId, string question, IList<Guid> materialIds, CancellationToken ct = default);
    Task<string> GenerateStudyGuideAsync(Guid subjectId, CancellationToken ct = default);
    Task<List<string>> GenerateTopicListAsync(Guid materialId, CancellationToken ct = default);
    Task SpeakTextAsync(string text, CancellationToken ct = default);
    Task<string> GenerateAudioOverviewAsync(Guid subjectId, CancellationToken ct = default);
}
