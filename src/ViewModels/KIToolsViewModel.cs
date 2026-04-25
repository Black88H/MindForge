using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using System.Collections.ObjectModel;

namespace MindForge.ViewModels;

public partial class KIToolsViewModel : ObservableObject
{
    private readonly IAISelector? _ai;

    public KIToolsViewModel() : this(null) { }
    public KIToolsViewModel(IAISelector? ai) => _ai = ai;

    // Upload
    [ObservableProperty] private string _uploadedFilePath = string.Empty;
    [ObservableProperty] private string _uploadedFileName = string.Empty;
    [ObservableProperty] private bool _hasUploadedFile = false;
    [ObservableProperty] private bool _isDragging = false;

    // Tabs
    [ObservableProperty] private string _selectedTab = "Fragen";
    public List<string> Tabs { get; } = ["Fragen generieren", "Zusammenfassung", "Custom Prompt"];

    // Processing
    [ObservableProperty] private bool _isProcessing = false;
    [ObservableProperty] private int _processingProgress = 0;
    [ObservableProperty] private string _processingStatus = string.Empty;
    [ObservableProperty] private string _estimatedTimeText = string.Empty;

    // Questions tab
    [ObservableProperty] private int _questionCount = 20;
    [ObservableProperty] private ObservableCollection<GeneratedQuestion> _generatedQuestions = new();

    // Summary tab
    [ObservableProperty] private string _summaryLength = "Mittel";
    public List<string> SummaryLengths { get; } = ["Kurz", "Mittel", "Lang"];
    [ObservableProperty] private string _summaryContent = string.Empty;

    // Custom prompt
    [ObservableProperty] private string _customPrompt = string.Empty;
    [ObservableProperty] private string _customResult = string.Empty;

    // Output
    [ObservableProperty] private string _outputFormat = "Markdown";
    public List<string> OutputFormats { get; } = ["Markdown", "PDF", "JSON"];
    [ObservableProperty] private string _previewContent = string.Empty;
    [ObservableProperty] private int _tokenCount = 0;
    [ObservableProperty] private decimal _estimatedCost = 0m;
    public string CostText => $"Tokens: {TokenCount} · Geschätzte Kosten: ${EstimatedCost:F3}";

    // XP toast
    [ObservableProperty] private bool _showXPToast = false;
    [ObservableProperty] private int _toastXP = 0;

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = tab;

    [RelayCommand]
    private void UploadFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Dokumente & Bilder|*.pdf;*.docx;*.txt;*.png;*.jpg;*.md|Alle|*.*"
        };
        if (dialog.ShowDialog() == true)
            LoadFile(dialog.FileName);
    }

    public void LoadFile(string path)
    {
        UploadedFilePath = path;
        UploadedFileName = System.IO.Path.GetFileName(path);
        HasUploadedFile = true;
    }

    [RelayCommand]
    private async Task ProcessAsync()
    {
        if (!HasUploadedFile && string.IsNullOrWhiteSpace(CustomPrompt)) return;

        IsProcessing = true;
        ProcessingProgress = 0;
        GeneratedQuestions.Clear();

        for (int i = 0; i <= 100; i += 10)
        {
            await Task.Delay(150);
            ProcessingProgress = i;
            ProcessingStatus = i < 100 ? $"Verarbeitung... {i}%" : "✓ Abgeschlossen";
        }

        if (SelectedTab.StartsWith("Fragen"))
        {
            for (int i = 1; i <= Math.Min(QuestionCount, 10); i++)
            {
                GeneratedQuestions.Add(new GeneratedQuestion
                {
                    Number = i,
                    Text = $"Frage {i} aus dem hochgeladenen Dokument",
                    Type = "Multiple Choice",
                    Difficulty = i % 3 == 0 ? "Schwer" : i % 2 == 0 ? "Mittel" : "Leicht"
                });
            }
            PreviewContent = $"## Generierte Fragen ({GeneratedQuestions.Count})\n\n" +
                string.Join("\n", GeneratedQuestions.Select(q => $"{q.Number}. {q.Text}"));
        }
        else if (SelectedTab.StartsWith("Zusammenfassung"))
        {
            SummaryContent = "**Zusammenfassung**\n\n• Wichtigster Punkt 1\n• Wichtigster Punkt 2\n• Wichtigster Punkt 3";
            PreviewContent = SummaryContent;
        }
        else
        {
            CustomResult = "KI-Antwort auf deine Anfrage...";
            PreviewContent = CustomResult;
        }

        TokenCount = Random.Shared.Next(200, 800);
        EstimatedCost = TokenCount * 0.000015m;
        IsProcessing = false;
        ShowXPToast = true;
        ToastXP = 150;
        await Task.Delay(3000);
        ShowXPToast = false;
    }

    [RelayCommand]
    private void CancelProcessing()
    {
        IsProcessing = false;
        ProcessingProgress = 0;
        ProcessingStatus = "Abgebrochen.";
    }

    [RelayCommand]
    private void Download()
    {
        var ext = OutputFormat switch { "PDF" => ".pdf", "JSON" => ".json", _ => ".md" };
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "mindforge-export" + ext,
            Filter = $"{OutputFormat}|*{ext}"
        };
        if (dialog.ShowDialog() == true)
            System.IO.File.WriteAllText(dialog.FileName, PreviewContent);
    }

    [RelayCommand]
    private void AddToLearningPlan()
    {
        // Would open plan selector dialog
    }
}

public class GeneratedQuestion
{
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "Multiple Choice";
    public string Difficulty { get; set; } = "Mittel";
    public bool IsSelected { get; set; } = true;
}
