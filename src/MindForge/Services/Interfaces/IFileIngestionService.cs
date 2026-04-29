using System;
using System.Threading.Tasks;
using MindForge.Models;
using MindForge.Services.Interfaces; // Für Result<T> falls es da definiert ist

namespace MindForge.Services.Interfaces;

public interface IFileIngestionService
{
    Task<Result<Material>> IngestPdfAsync(Guid userId, Guid subjectId, string filePath, string title);
    Task<string> ExtractTextFromPdfAsync(string filePath);
}
