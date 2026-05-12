using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MindForge.Models;

namespace MindForge.Services.Interfaces;

public interface IAnnotationService
{
    Task<Annotation> CreateAsync(Guid materialId, Guid userId, string selectedText, AnnotationType type, string userNote = "");
    Task<List<Annotation>> GetForMaterialAsync(Guid materialId);
    Task<List<Annotation>> GetForUserAsync(Guid userId, AnnotationType? filter = null);
    Task DeleteAsync(Guid annotationId);
    Task<string> GenerateSmartNoteAsync(string text, AnnotationType type);
}
