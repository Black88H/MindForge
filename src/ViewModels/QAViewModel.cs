using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;

namespace MindForge.ViewModels;

public partial class QAViewModel : ObservableObject
{
    private readonly IAISelector? _aiSelector;

    public QAViewModel(IAISelector? aiSelector = null)
    {
        _aiSelector = aiSelector;
    }

    [ObservableProperty] private string _subjectName = "Analysis II";
    [ObservableProperty] private string _subjectColor = "#5B8CFF";
    [ObservableProperty] private string _questionTag = "Reihen";
    [ObservableProperty] private string _difficultyTag = "Mittel";
    [ObservableProperty] private string _questionNumber = "#047 · 13 falsch";
    [ObservableProperty] private string _questionText = "Welche Reihe konvergiert nach dem Leibniz-Kriterium, aber nicht absolut?";
    [ObservableProperty] private List<AnswerOption> _options = new()
    {
        new() { Label="A", Text="∑ 1/n²" },
        new() { Label="B", Text="∑ (-1)ⁿ / n" },
        new() { Label="C", Text="∑ (-1)ⁿ / n²" },
        new() { Label="D", Text="∑ 1/√n" },
    };

    [ObservableProperty] private AnswerOption? _selectedOption;
    [ObservableProperty] private bool _isAnswered = false;
    [ObservableProperty] private bool _showExplanation = false;
    [ObservableProperty] private bool _isCorrect = false;
    [ObservableProperty] private string _explanation = "Die Reihe ∑ (-1)ⁿ / n ist nach dem Leibniz-Kriterium konvergent (alternierende Reihe, Glieder monoton fallend gegen 0). Da ∑ 1/n (harmonische Reihe) divergiert, ist sie nicht absolut konvergent.";
    [ObservableProperty] private int _questionProgress = 7;
    [ObservableProperty] private int _questionTotal = 20;
    [ObservableProperty] private bool _isLoadingExplanation = false;

    // AI Status
    [ObservableProperty] private string _aiProviderName = string.Empty;
    [ObservableProperty] private int _lastTokenCount = 0;
    [ObservableProperty] private string _lastCostText = string.Empty;

    public double SessionProgress => QuestionTotal > 0 ? (double)QuestionProgress / QuestionTotal : 0;
    public string SessionProgressText => $"{QuestionProgress} / {QuestionTotal}";

    [RelayCommand]
    private void SelectOption(AnswerOption option)
    {
        if (IsAnswered) return;
        SelectedOption = option;
        foreach (var o in Options) o.IsSelected = o == option;
    }

    [RelayCommand]
    private void SubmitAnswer()
    {
        if (SelectedOption == null || IsAnswered) return;
        IsAnswered = true;
        IsCorrect = SelectedOption.Label == "B";
        foreach (var o in Options)
        {
            o.IsCorrect = o.Label == "B";
            o.IsWrong = o == SelectedOption && !IsCorrect;
        }
    }

    [RelayCommand]
    private async Task ShowAIExplanationAsync()
    {
        IsLoadingExplanation = true;

        if (_aiSelector != null)
        {
            var response = await _aiSelector.ExecuteAsync(TaskType.QAExplanation, QuestionText);
            if (response.IsSuccess)
            {
                Explanation      = response.Content;
                AiProviderName   = response.ProviderName;
                LastTokenCount   = response.TokensUsed;
                LastCostText     = CostCalculator.FormatCost(response.CostUSD);
            }
            else
            {
                Explanation = $"KI-Fehler: {response.Error}";
                AiProviderName = response.ProviderName;
            }
        }
        else
        {
            await Task.Delay(800);
        }

        IsLoadingExplanation = false;
        ShowExplanation = true;
    }

    [RelayCommand]
    private void NextQuestion()
    {
        QuestionProgress++;
        IsAnswered = false;
        SelectedOption = null;
        ShowExplanation = false;
        AiProviderName = string.Empty;
        LastTokenCount = 0;
        LastCostText = string.Empty;
        foreach (var o in Options) { o.IsSelected = false; o.IsCorrect = false; o.IsWrong = false; }
    }

    [RelayCommand]
    private void ShuffleQuestion() => NextQuestion();
}

public partial class AnswerOption : ObservableObject
{
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private bool _isSelected = false;
    [ObservableProperty] private bool _isCorrect = false;
    [ObservableProperty] private bool _isWrong = false;
}
