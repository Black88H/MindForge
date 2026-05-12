using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public record DocSimilarity(string Topic, string Description, double Confidence);
public record DocDifference(string Aspect, string Doc1View, string Doc2View, string Significance);
public record ComparisonResult(
    string Doc1Name,
    string Doc2Name,
    List<DocSimilarity> Similarities,
    List<DocDifference> Differences,
    string ComplementaryInsights,
    string RecommendedReadingOrder);

public interface IDocumentComparisonService
{
    Task<ComparisonResult> CompareAsync(Guid materialId1, Guid materialId2);
}
