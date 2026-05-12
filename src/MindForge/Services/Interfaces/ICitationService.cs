using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public interface ICitationService
{
    Task<Citation> CreateFromMaterialAsync(Guid materialId, Guid userId);
    Task<List<Citation>> GetForNotebookAsync(Guid notebookId);
    Task<string> GenerateBibliographyAsync(Guid notebookId, string format = "APA");
}
