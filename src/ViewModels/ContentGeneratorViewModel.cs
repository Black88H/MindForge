using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MindForge.ViewModels;

public partial class ContentGeneratorViewModel : ObservableObject
{
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

    [RelayCommand]
    private async Task UploadFileAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "PDF Dateien (*.pdf)|*.pdf|Alle Dateien (*.*)|*.*",
            Title = "PDF hochladen"
        };
        if (dialog.ShowDialog() == true)
        {
            UploadedFileName = System.IO.Path.GetFileName(dialog.FileName);
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
        await Task.Delay(600);
        ProcessingStatus = "Text wird extrahiert...";
        ProcessingProgress = 0.4;
        await Task.Delay(800);
        ProcessingStatus = "KI generiert Inhalte...";
        ProcessingProgress = 0.75;
        await Task.Delay(1200);
        ProcessingStatus = "Fertig!";
        ProcessingProgress = 1.0;
        await Task.Delay(300);
        IsProcessing = false;
        HasOutput = true;
        XpEarned = 150;
        PreviewContent = GeneratePreview();
        ShowXpPopup = true;
        await Task.Delay(2500);
        ShowXpPopup = false;
    }

    [RelayCommand]
    private async Task DownloadOutputAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = OutputFormat == "PDF" ? "PDF (*.pdf)|*.pdf" :
                     OutputFormat == "JSON" ? "JSON (*.json)|*.json" : "Markdown (*.md)|*.md",
            FileName = $"MindForge_Export_{DateTime.Now:yyyyMMdd}"
        };
        if (dialog.ShowDialog() == true)
        {
            await File.WriteAllTextAsync(dialog.FileName, PreviewContent);
        }
    }

    private string GeneratePreview() => ActiveTab switch
    {
        "Fragen" => "## Generierte Fragen\n\n**Frage 1:** Was ist das Leibniz-Kriterium?\n- A) ...\n- B) ...\n\n**Frage 2:** ...",
        "Zusammenfassung" => "## Zusammenfassung\n\nDer Text behandelt...",
        _ => "## Custom Output\n\n..."
    };
}
