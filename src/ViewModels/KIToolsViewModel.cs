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

    // View-facing upload alias
    public string FileName => UploadedFileName;
    partial void OnUploadedFileNameChanged(string value) => OnPropertyChanged(nameof(FileName));

    // Text input
    [ObservableProperty] private string _inputText = string.Empty;

    // Output format radios
    [ObservableProperty] private bool _isFlashcardMode = true;
    [ObservableProperty] private bool _isMcMode = false;
    [ObservableProperty] private bool _isOpenMode = false;

    // Config
    [ObservableProperty] private int _generateCount = 20;
    [ObservableProperty] private string _subjectHint = string.Empty;

    // Tabs (internal)
    [ObservableProperty] private string _selectedTab = "Fragen";
    public List<string> Tabs { get; } = ["Fragen generieren", "Zusammenfassung", "Custom Prompt"];

    // Processing
    [ObservableProperty] private bool _isProcessing = false;
    [ObservableProperty] private int _processingProgress = 0;
    [ObservableProperty] private string _processingStatus = string.Empty;
    [ObservableProperty] private string _estimatedTimeText = string.Empty;

    // View-facing processing aliases
    public bool IsGenerating => IsProcessing;
    partial void OnIsProcessingChanged(bool value) => OnPropertyChanged(nameof(IsGenerating));

    public double GenerateProgress => ProcessingProgress / 100.0;
    partial void OnProcessingProgressChanged(int value) => OnPropertyChanged(nameof(GenerateProgress));

    public string StatusText => ProcessingStatus;
    partial void OnProcessingStatusChanged(string value) => OnPropertyChanged(nameof(StatusText));

    // Output
    [ObservableProperty] private bool _hasOutput = false;
    [ObservableProperty] private int _outputTab = 0;

    // Questions tab
    [ObservableProperty] private int _questionCount = 20;
    [ObservableProperty] private ObservableCollection<GeneratedQuestion> _generatedQuestions = new();

    // Summary tab
    [ObservableProperty] private string _summaryLength = "Mittel";
    public List<string> SummaryLengths { get; } = ["Kurz", "Mittel", "Lang"];
    [ObservableProperty] private string _summaryContent = string.Empty;
    public string SummaryText => SummaryContent;
    partial void OnSummaryContentChanged(string value) => OnPropertyChanged(nameof(SummaryText));

    // Custom prompt tab
    [ObservableProperty] private string _customPrompt = string.Empty;
    [ObservableProperty] private string _customResult = string.Empty;
    public string CustomOutput => CustomResult;
    partial void OnCustomResultChanged(string value) => OnPropertyChanged(nameof(CustomOutput));

    // Export
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
    private void SelectOutputTab(string param)
    {
        if (int.TryParse(param, out int i)) OutputTab = i;
    }

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
    private void Generate() => _ = ProcessAsync();

    [RelayCommand]
    private void RunCustom() => _ = ProcessAsync();

    [RelayCommand]
    private async Task ProcessAsync()
    {
        if (!HasUploadedFile && string.IsNullOrWhiteSpace(InputText) && string.IsNullOrWhiteSpace(CustomPrompt)) return;

        IsProcessing = true;
        ProcessingProgress = 0;
        GeneratedQuestions.Clear();

        for (int i = 0; i <= 100; i += 10)
        {
            await Task.Delay(150);
            ProcessingProgress = i;
            ProcessingStatus = i < 100 ? $"Verarbeitung... {i}%" : "✓ Abgeschlossen";
        }

        var count = GenerateCount > 0 ? GenerateCount : QuestionCount;

        if (IsFlashcardMode || IsMcMode || IsOpenMode || SelectedTab.StartsWith("Fragen"))
        {
            for (int i = 1; i <= Math.Min(count, 10); i++)
            {
                GeneratedQuestions.Add(new GeneratedQuestion
                {
                    Number = i,
                    Text = $"Frage {i} aus dem hochgeladenen Dokument",
                    Type = IsMcMode ? "Multiple Choice" : IsFlashcardMode ? "Karteikarte" : "Offen",
                    Difficulty = i % 3 == 0 ? "Schwer" : i % 2 == 0 ? "Mittel" : "Leicht"
                });
            }
            PreviewContent = $"## Generierte Fragen ({GeneratedQuestions.Count})\n\n" +
                string.Join("\n", GeneratedQuestions.Select(q => $"{q.Number}. {q.Text}"));
            OutputTab = 0;
        }
        else if (SelectedTab.StartsWith("Zusammenfassung"))
        {
            SummaryContent = "**Zusammenfassung**\n\n• Wichtigster Punkt 1\n• Wichtigster Punkt 2\n• Wichtigster Punkt 3";
            PreviewContent = SummaryContent;
            OutputTab = 1;
        }
        else
        {
            CustomResult = "KI-Antwort auf deine Anfrage...";
            PreviewContent = CustomResult;
            OutputTab = 2;
        }

        TokenCount = Random.Shared.Next(200, 800);
        EstimatedCost = TokenCount * 0.000015m;
        HasOutput = true;
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
    private void Export()
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
    private void SaveToLibrary()
    {
        // Would open plan selector dialog
    }

    [RelayCommand]
    private void Download() => ExportCommand.Execute(null);

    [RelayCommand]
    private void AddToLearningPlan() { }
}

public class GeneratedQuestion
{
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "Multiple Choice";
    public string Difficulty { get; set; } = "Mittel";
    public bool IsSelected { get; set; } = true;
    public string Question => Text;
    public string Answer => $"Antwort {Number}";
}
