using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;

namespace MindForge.ViewModels;

public partial class KIToolsViewModel : ObservableObject
{
    private readonly IAISelector _ai;

    public KIToolsViewModel(IAISelector ai)
    {
        _ai = ai;
    }

    // Upload
    [ObservableProperty] private string _uploadedFilePath = string.Empty;
    [ObservableProperty] private string _uploadedFileName = string.Empty;
    [ObservableProperty] private bool _hasUploadedFile = false;
    [ObservableProperty] private bool _isDragging = false;

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
    partial void OnTokenCountChanged(int value) => OnPropertyChanged(nameof(CostText));
    partial void OnEstimatedCostChanged(decimal value) => OnPropertyChanged(nameof(CostText));

    // Errors
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _aiProviderName = string.Empty;

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
        if (System.IO.Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".md")
        {
            try { InputText = System.IO.File.ReadAllText(path); }
            catch { /* ignore */ }
        }
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        var source = !string.IsNullOrWhiteSpace(InputText) ? InputText
                   : HasUploadedFile ? $"(Material: {UploadedFileName})" : string.Empty;
        if (string.IsNullOrWhiteSpace(source)) { ErrorMessage = "Bitte zuerst Material hochladen oder Text eingeben."; return; }

        ErrorMessage = string.Empty;
        IsProcessing = true;
        ProcessingProgress = 10;
        ProcessingStatus = "KI wird angefragt...";
        GeneratedQuestions.Clear();

        try
        {
            if (SelectedTab.StartsWith("Fragen") || IsFlashcardMode || IsMcMode || IsOpenMode)
            {
                var modeLabel = IsMcMode ? "Multiple Choice" : IsFlashcardMode ? "Karteikarten" : "Offene Fragen";
                var prompt = $@"Erstelle {GenerateCount} {modeLabel}-Fragen basierend auf diesem Inhalt.
Antworte ausschließlich als JSON-Array. Jedes Element:
{{ ""question"": ""..."", ""answer"": ""..."", ""difficulty"": ""Leicht|Mittel|Schwer"" }}

Inhalt:
{source}";

                ProcessingProgress = 40;
                var resp = await _ai.ExecuteAsync(TaskType.ContentGeneration, prompt);
                ProcessingProgress = 80;
                if (!resp.IsSuccess) { ErrorMessage = $"KI-Fehler: {resp.Error}"; ProcessingStatus = "✗ Fehler"; return; }

                AiProviderName = resp.ProviderName;
                TokenCount     = resp.TokensUsed;
                EstimatedCost  = (decimal)resp.CostUSD;

                var parsed = TryParseQuestions(resp.Content);
                int n = 1;
                foreach (var q in parsed)
                    GeneratedQuestions.Add(new GeneratedQuestion
                    {
                        Number = n++,
                        Text = q.Question,
                        Type = modeLabel,
                        Difficulty = q.Difficulty,
                        Answer = q.Answer,
                    });

                if (GeneratedQuestions.Count == 0)
                {
                    // Model didn't return JSON — surface raw content
                    PreviewContent = resp.Content;
                }
                else
                {
                    PreviewContent = $"## Generierte Fragen ({GeneratedQuestions.Count})\n\n" +
                        string.Join("\n", GeneratedQuestions.Select(q => $"{q.Number}. {q.Text}\n   ✓ {q.Answer}"));
                }
                OutputTab = 0;
            }
            else if (SelectedTab.StartsWith("Zusammenfassung"))
            {
                var lengthHint = SummaryLength switch
                {
                    "Kurz" => "in 3-5 Bullet Points",
                    "Lang" => "in 8-12 detaillierten Absätzen",
                    _      => "in 5-7 strukturierten Bullet Points"
                };
                var prompt = $"Fasse den folgenden Inhalt {lengthHint} auf Deutsch zusammen.\n\n{source}";
                ProcessingProgress = 50;
                var resp = await _ai.ExecuteAsync(TaskType.ContentGeneration, prompt);
                if (!resp.IsSuccess) { ErrorMessage = $"KI-Fehler: {resp.Error}"; ProcessingStatus = "✗ Fehler"; return; }
                AiProviderName = resp.ProviderName;
                TokenCount     = resp.TokensUsed;
                EstimatedCost  = (decimal)resp.CostUSD;
                SummaryContent = resp.Content;
                PreviewContent = SummaryContent;
                OutputTab = 1;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(CustomPrompt)) { ErrorMessage = "Bitte einen Prompt eingeben."; return; }
                var prompt = $"{CustomPrompt}\n\nKontext:\n{source}";
                ProcessingProgress = 50;
                var resp = await _ai.ExecuteAsync(TaskType.QAExplanation, prompt);
                if (!resp.IsSuccess) { ErrorMessage = $"KI-Fehler: {resp.Error}"; ProcessingStatus = "✗ Fehler"; return; }
                AiProviderName = resp.ProviderName;
                TokenCount     = resp.TokensUsed;
                EstimatedCost  = (decimal)resp.CostUSD;
                CustomResult   = resp.Content;
                PreviewContent = CustomResult;
                OutputTab = 2;
            }

            ProcessingProgress = 100;
            ProcessingStatus = $"✓ Erstellt mit {AiProviderName} · {CostCalculator.FormatCost((double)EstimatedCost)}";
            HasOutput = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unerwarteter Fehler: {ex.Message}";
            ProcessingStatus = "✗ Fehler";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task RunCustomAsync() => await GenerateAsync();

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
        // Library persistence is part of Phase 1 (MaterialLibrary table will be wired then)
    }

    [RelayCommand]
    private void Download() => ExportCommand.Execute(null);

    [RelayCommand]
    private void AddToLearningPlan() { }

    private static List<ParsedQuestion> TryParseQuestions(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new();

        // Strip code fences if model wrapped output
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNl = cleaned.IndexOf('\n');
            if (firstNl > 0) cleaned = cleaned[(firstNl + 1)..];
            var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence > 0) cleaned = cleaned[..lastFence];
            cleaned = cleaned.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(cleaned);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return new();
            var result = new List<ParsedQuestion>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                result.Add(new ParsedQuestion
                {
                    Question = el.TryGetProperty("question", out var qv) ? qv.GetString() ?? "" : "",
                    Answer   = el.TryGetProperty("answer",   out var av) ? av.GetString() ?? "" : "",
                    Difficulty = el.TryGetProperty("difficulty", out var dv) ? dv.GetString() ?? "Mittel" : "Mittel",
                });
            }
            return result;
        }
        catch
        {
            return new();
        }
    }

    private record ParsedQuestion
    {
        public string Question { get; init; } = string.Empty;
        public string Answer { get; init; } = string.Empty;
        public string Difficulty { get; init; } = "Mittel";
    }
}

public class GeneratedQuestion
{
    public int Number { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Type { get; set; } = "Multiple Choice";
    public string Difficulty { get; set; } = "Mittel";
    public bool IsSelected { get; set; } = true;
    public string Answer { get; set; } = string.Empty;
    public string Question => Text;
}
