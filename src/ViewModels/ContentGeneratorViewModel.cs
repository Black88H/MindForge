using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;

namespace MindForge.ViewModels;

public partial class ContentGeneratorViewModel : ObservableObject
{
    private readonly IAISelector _aiSelector;
    private string _rawFileContent = string.Empty;

    public ContentGeneratorViewModel(IAISelector aiSelector)
    {
        _aiSelector = aiSelector;
    }

    [ObservableProperty] private string _uploadedFileName = string.Empty;
    [ObservableProperty] private bool _hasFile = false;
    [ObservableProperty] private bool _isProcessing = false;
    [ObservableProperty] private double _processingProgress = 0;
    [ObservableProperty] private string _processingStatus = string.Empty;
    [ObservableProperty] private string _activeTab = "Fragen";
    [ObservableProperty] private int _questionCount = 20;
    [ObservableProperty] private string _difficulty = "Mittel";
    [ObservableProperty] private string _outputFormat = "Markdown";
    [ObservableProperty] private string _previewContent = string.Empty;
    [ObservableProperty] private bool _hasOutput = false;
    [ObservableProperty] private string _customPrompt = string.Empty;
    [ObservableProperty] private int _xpEarned = 0;
    [ObservableProperty] private bool _showXpPopup = false;

    // AI Status
    [ObservableProperty] private string _aiProviderName = string.Empty;
    [ObservableProperty] private string _tokenInfo = string.Empty;

    [RelayCommand]
    private async Task UploadFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PDF Dateien (*.pdf)|*.pdf|Alle Dateien (*.*)|*.*",
            Title  = "PDF hochladen"
        };
        if (dialog.ShowDialog() == true)
        {
            UploadedFileName = Path.GetFileName(dialog.FileName);
            _rawFileContent  = $"Datei: {UploadedFileName}";
            HasFile = true;
            await ProcessFileAsync();
        }
    }

    [RelayCommand]
    private async Task ProcessFileAsync()
    {
        if (!HasFile) return;
        IsProcessing = true;
        ProcessingStatus = "Datei wird gelesen...";
        ProcessingProgress = 0.1;
        await Task.Delay(400);

        ProcessingStatus = "Text wird extrahiert...";
        ProcessingProgress = 0.4;
        await Task.Delay(500);

        ProcessingStatus = "KI generiert Inhalte...";
        ProcessingProgress = 0.75;

        var response = await _aiSelector.ExecuteAsync(
            TaskType.ContentGeneration,
            _rawFileContent,
            ActiveTab);

        if (response.IsSuccess)
        {
            PreviewContent  = response.Content;
            AiProviderName  = response.ProviderName;
            TokenInfo       = $"{response.TokensUsed} Tokens · {CostCalculator.FormatCost(response.CostUSD)}";
        }
        else
        {
            PreviewContent = $"## Fehler\n\n{response.Error}\n\nBitte prüfe die KI-Konfiguration unter Einstellungen → KI & Provider.";
            AiProviderName = $"Fehler: {response.Error}";
        }

        ProcessingStatus   = "Fertig!";
        ProcessingProgress = 1.0;
        await Task.Delay(300);

        IsProcessing = false;
        HasOutput    = true;
        XpEarned     = 150;
        ShowXpPopup  = true;
        await Task.Delay(2500);
        ShowXpPopup = false;
    }

    [RelayCommand]
    private async Task DownloadOutputAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = OutputFormat == "PDF"  ? "PDF (*.pdf)|*.pdf" :
                       OutputFormat == "JSON" ? "JSON (*.json)|*.json" : "Markdown (*.md)|*.md",
            FileName = $"MindForge_Export_{DateTime.Now:yyyyMMdd}"
        };
        if (dialog.ShowDialog() == true)
            await File.WriteAllTextAsync(dialog.FileName, PreviewContent);
    }

}
