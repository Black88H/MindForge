using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class MaterialLibraryViewModel : ObservableObject
{
    private readonly IFileIngestionService _ingest;
    private readonly IKnowledgeGraphService _graph;
    private readonly MindForgeDbContext _db;

    private Guid _currentSubjectId = Guid.Empty;

    [ObservableProperty] private ObservableCollection<Material> _materials = new();
    [ObservableProperty] private bool _isProcessing = false;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public bool HasMaterials => Materials.Count > 0;

    public MaterialLibraryViewModel(IFileIngestionService ingest, IKnowledgeGraphService graph, MindForgeDbContext db)
    {
        _ingest = ingest;
        _graph  = graph;
        _db     = db;
    }

    public async Task InitializeAsync(Guid subjectId = default)
    {
        _currentSubjectId = subjectId;
        await LoadMaterialsAsync();
    }

    private async Task LoadMaterialsAsync()
    {
        Materials.Clear();
        IQueryable<Material> query = _db.Materials.Where(m => m.UserId == UserSession.UserId);
        if (_currentSubjectId != Guid.Empty)
            query = query.Where(m => m.SubjectId == _currentSubjectId);

        var list = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
        foreach (var m in list) Materials.Add(m);
        OnPropertyChanged(nameof(HasMaterials));
    }

    /// <summary>Invoked from drag-drop in code-behind.</summary>
    public async Task UploadDroppedFileAsync(string filePath)
    {
        if (_currentSubjectId == Guid.Empty)
        {
            StatusMessage = "Bitte zuerst ein Fach auswählen.";
            return;
        }
        await IngestFileAsync(filePath);
    }

    [RelayCommand]
    private async Task UploadMaterialAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Lernmaterial|*.pdf;*.docx;*.png;*.jpg;*.jpeg",
            Title  = "Material hochladen"
        };
        if (dialog.ShowDialog() != true) return;
        await IngestFileAsync(dialog.FileName);
    }

    private async Task IngestFileAsync(string filePath)
    {
        IsProcessing = true;
        StatusMessage = string.Empty;
        try
        {
            var subjectId = _currentSubjectId != Guid.Empty
                ? _currentSubjectId
                : Guid.Empty;    // Service may handle empty-subject

            var material = await _ingest.IngestFileAsync(filePath, subjectId, UserSession.UserId);
            Materials.Insert(0, material);
            OnPropertyChanged(nameof(HasMaterials));

            // Build knowledge graph in background
            _ = Task.Run(() => _graph.BuildGraphFromMaterialAsync(material.Id));
            StatusMessage = $"✓ {material.OriginalFileName} verarbeitet ({material.TokenCount} Tokens)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
