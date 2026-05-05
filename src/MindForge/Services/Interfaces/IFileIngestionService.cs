using System;
using System.Threading;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public interface IFileIngestionService
{
    Task<Result<Material>> IngestPdfAsync(Guid userId, Guid subjectId, string filePath, string title);
    Task<Result<Material>> IngestDocxAsync(Guid userId, Guid subjectId, string filePath, string title);
    Task<Result<Material>> IngestTextAsync(Guid userId, Guid subjectId, string filePath, string title);
    Task<Result<Material>> IngestWebUrlAsync(Guid userId, Guid subjectId, string url, string title, CancellationToken ct = default);
    Task<Result<Material>> IngestImageAsync(Guid userId, Guid subjectId, string filePath, string title);
    Task<string> ExtractTextFromPdfAsync(string filePath);
}
