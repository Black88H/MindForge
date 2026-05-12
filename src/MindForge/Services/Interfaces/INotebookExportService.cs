using System;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public interface INotebookExportService
{
    /// <summary>Export a notebook and all its materials/chat as a .mindforge zip file.</summary>
    Task<string> ExportAsync(Guid notebookId, string destinationFolder);

    /// <summary>Import a .mindforge zip file into the current user's account.</summary>
    Task<Guid> ImportAsync(string zipFilePath, Guid userId);
}
