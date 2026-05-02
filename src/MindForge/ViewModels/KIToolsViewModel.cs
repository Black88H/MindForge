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
using System.Threading;
using System.Threading.Tasks;

namespace MindForge.ViewModels;

public partial class KIToolsViewModel : ObservableObject
{
    private readonly MindForgeDbContext _db;
    private readonly INotebookService _notebook;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private ObservableCollection<Subject> _subjects = new();
    [ObservableProperty] private ObservableCollection<Material> _subjectMaterials = new();
    [ObservableProperty] private ObservableCollection<Material> _selectedMaterials = new();
    [ObservableProperty] private Subject? _selectedSubject;
    [ObservableProperty] private string _questionInput = string.Empty;
    [ObservableProperty] private string _output = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _activeToolName = string.Empty;
    [ObservableProperty] private bool _isAudioPlaying;

    public KIToolsViewModel(MindForgeDbContext db, INotebookService notebook)
    {
        _db = db;
        _notebook = notebook;
    }

    partial void OnSelectedSubjectChanged(Subject? value) => _ = LoadSubjectMaterialsAsync();

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

    public async Task LoadSubjectMaterialsAsync()
    {
        if (SelectedSubject == null) return;
        var list = await _db.Materials
            .Where(m => m.SubjectId == SelectedSubject.Id)
            .OrderBy(m => m.OriginalFileName)
            .ToListAsync();
        SubjectMaterials = new ObservableCollection<Material>(list);
        SelectedMaterials.Clear();
    }

    [RelayCommand]
    public async Task SummarizeSubjectAsync()
    {
        if (SelectedSubject == null) return;
        await RunToolAsync("Subject Summary", ct =>
            _notebook.SummarizeSubjectAsync(SelectedSubject.Id, ct: ct));
    }

    [RelayCommand]
    public async Task AskWithSourcesAsync()
    {
        if (string.IsNullOrWhiteSpace(QuestionInput)) return;

        var ids = SelectedMaterials.Select(m => m.Id).ToList();
        await RunToolAsync("Q&A with Sources", async ct =>
        {
            var result = await _notebook.AskWithSourcesAsync(
                UserSession.UserId, QuestionInput, ids, ct);

            var citations = result.Citations.Count > 0
                ? "\n\n📎 Sources:\n" + string.Join("\n", result.Citations.Select(
                    c => $"  • {c.MaterialTitle}: \"{c.Excerpt}\""))
                : string.Empty;

            return result.Answer + citations;
        });
    }

    [RelayCommand]
    public async Task GenerateStudyGuideAsync()
    {
        if (SelectedSubject == null) return;
        await RunToolAsync("Study Guide", ct =>
            _notebook.GenerateStudyGuideAsync(SelectedSubject.Id, ct));
    }

    [RelayCommand]
    public async Task GenerateAudioOverviewAsync()
    {
        if (SelectedSubject == null) return;
        await RunToolAsync("Audio Script", ct =>
            _notebook.GenerateAudioOverviewAsync(SelectedSubject.Id, ct));
    }

    [RelayCommand]
    public async Task SpeakOutputAsync()
    {
        if (string.IsNullOrWhiteSpace(Output) || IsAudioPlaying) return;

        IsAudioPlaying = true;
        StatusMessage = "🔊 Speaking...";
        _cts = new CancellationTokenSource();

        try
        {
            await _notebook.SpeakTextAsync(Output, _cts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusMessage = $"❌ TTS error: {ex.Message}";
        }
        finally
        {
            IsAudioPlaying = false;
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    public void StopSpeaking()
    {
        _cts?.Cancel();
        IsAudioPlaying = false;
    }

    public void ToggleMaterialSelection(Material material)
    {
        if (SelectedMaterials.Contains(material))
            SelectedMaterials.Remove(material);
        else
            SelectedMaterials.Add(material);
    }

    private async Task RunToolAsync(string toolName, Func<CancellationToken, Task<string>> action)
    {
        IsBusy = true;
        ActiveToolName = toolName;
        Output = $"⏳ Running {toolName}...";
        StatusMessage = "Calling AI...";
        _cts = new CancellationTokenSource();

        try
        {
            Output = await action(_cts.Token);
            StatusMessage = $"✅ {toolName} complete.";
        }
        catch (OperationCanceledException)
        {
            Output = "Cancelled.";
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            Output = $"Error: {ex.Message}";
            StatusMessage = "❌ Failed.";
        }
        finally { IsBusy = false; }
    }
}
