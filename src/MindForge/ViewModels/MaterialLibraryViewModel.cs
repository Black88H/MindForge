using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MindForge.ViewModels;

public partial class MaterialLibraryViewModel : ObservableObject
{
    private readonly MindForgeDbContext _db;
    private readonly IFileIngestionService _ingestion;
    private readonly INotebookService _notebook;

    [ObservableProperty] private ObservableCollection<Subject> _subjects = new();
    [ObservableProperty] private ObservableCollection<Material> _materials = new();
    [ObservableProperty] private Subject? _selectedSubject;
    [ObservableProperty] private Material? _selectedMaterial;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _aiOutput = string.Empty;
    [ObservableProperty] private string _filterText = string.Empty;

    public MaterialLibraryViewModel(MindForgeDbContext db, IFileIngestionService ingestion, INotebookService notebook)
    {
        _db = db;
        _ingestion = ingestion;
        _notebook = notebook;
    }

    partial void OnSelectedSubjectChanged(Subject? value) => _ = LoadMaterialsAsync();
    partial void OnFilterTextChanged(string value) => _ = LoadMaterialsAsync();

    public async Task LoadSubjectsAsync()
    {
        var list = await _db.Subjects
            .Where(s => s.UserId == UserSession.UserId)
            .OrderBy(s => s.Name)
            .ToListAsync();
        Subjects = new ObservableCollection<Subject>(list);
        if (Subjects.Count > 0 && SelectedSubject == null)
            SelectedSubject = Subjects[0];
    }

    public async Task LoadMaterialsAsync()
    {
        if (SelectedSubject == null) return;

        var query = _db.Materials
            .Where(m => m.SubjectId == SelectedSubject.Id);

        if (!string.IsNullOrWhiteSpace(FilterText))
            query = query.Where(m => m.OriginalFileName.Contains(FilterText));

        var list = await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        Materials = new ObservableCollection<Material>(list);
    }

    [RelayCommand]
    public async Task IngestFileAsync(string filePath)
    {
        if (SelectedSubject == null || string.IsNullOrEmpty(filePath)) return;

        IsBusy = true;
        StatusMessage = "Ingesting file...";

        var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        var title = System.IO.Path.GetFileNameWithoutExtension(filePath);

        Result<Material> result = ext switch
        {
            ".pdf"  => await _ingestion.IngestPdfAsync(UserSession.UserId, SelectedSubject.Id, filePath, title),
            ".docx" => await _ingestion.IngestDocxAsync(UserSession.UserId, SelectedSubject.Id, filePath, title),
            ".txt" or ".md" => await _ingestion.IngestTextAsync(UserSession.UserId, SelectedSubject.Id, filePath, title),
            _ => Result<Material>.Failure($"Unsupported file type: {ext}")
        };

        StatusMessage = result.IsSuccess ? $"✅ '{title}' added successfully." : $"❌ {result.ErrorMessage}";
        IsBusy = false;

        if (result.IsSuccess)
            await LoadMaterialsAsync();
    }

    [RelayCommand]
    public async Task IngestUrlAsync(string url)
    {
        if (SelectedSubject == null || string.IsNullOrEmpty(url)) return;

        IsBusy = true;
        StatusMessage = "Fetching web content...";

        var uri = new Uri(url);
        var title = uri.Host + uri.AbsolutePath;
        var result = await _ingestion.IngestWebUrlAsync(UserSession.UserId, SelectedSubject.Id, url, title);

        StatusMessage = result.IsSuccess ? "✅ Web content added." : $"❌ {result.ErrorMessage}";
        IsBusy = false;

        if (result.IsSuccess)
            await LoadMaterialsAsync();
    }

    [RelayCommand]
    public async Task DeleteMaterialAsync()
    {
        if (SelectedMaterial == null) return;

        var m = await _db.Materials.FindAsync(SelectedMaterial.Id);
        if (m != null)
        {
            _db.Materials.Remove(m);
            await _db.SaveChangesAsync();
            await LoadMaterialsAsync();
            StatusMessage = "Material deleted.";
        }
    }

    [RelayCommand]
    public async Task SummarizeMaterialAsync()
    {
        if (SelectedMaterial == null) return;

        IsBusy = true;
        AiOutput = "Generating summary...";
        StatusMessage = "Calling AI...";

        try
        {
            AiOutput = await _notebook.SummarizeMaterialAsync(SelectedMaterial.Id);
            StatusMessage = "✅ Summary generated.";
        }
        catch (Exception ex)
        {
            AiOutput = $"Error: {ex.Message}";
            StatusMessage = "❌ Summary failed.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    public async Task GenerateTopicsAsync()
    {
        if (SelectedMaterial == null) return;

        IsBusy = true;
        AiOutput = "Extracting topics...";

        try
        {
            var topics = await _notebook.GenerateTopicListAsync(SelectedMaterial.Id);
            AiOutput = string.Join("\n", topics.Select((t, i) => $"{i + 1}. {t}"));
            StatusMessage = "✅ Topics extracted.";
        }
        catch (Exception ex)
        {
            AiOutput = $"Error: {ex.Message}";
            StatusMessage = "❌ Failed.";
        }
        finally { IsBusy = false; }
    }
}
