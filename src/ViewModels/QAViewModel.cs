using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;

namespace MindForge.ViewModels;

public partial class QAViewModel : ObservableObject
{
    private readonly IAISelector _aiSelector;
    private readonly QuestionRepository _questions;
    private readonly UserProgressRepository _progress;
    private readonly MindForgeDbContext _db;

    private readonly Queue<Question> _queue = new();
    private Question? _current;

    public QAViewModel(
        IAISelector aiSelector,
        QuestionRepository questions,
        UserProgressRepository progress,
        MindForgeDbContext db)
    {
        _aiSelector = aiSelector;
        _questions = questions;
        _progress = progress;
        _db = db;
        _ = LoadQueueAsync();
    }

    [ObservableProperty] private string _subjectName = string.Empty;
    [ObservableProperty] private string _subjectColor = "#5B8CFF";
    [ObservableProperty] private string _questionTag = string.Empty;
    [ObservableProperty] private string _difficultyTag = string.Empty;
    [ObservableProperty] private string _questionNumber = string.Empty;
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private ObservableCollection<AnswerOption> _options = new();

    [ObservableProperty] private AnswerOption? _selectedOption;
    [ObservableProperty] private bool _isAnswered = false;
    [ObservableProperty] private bool _showExplanation = false;
    [ObservableProperty] private bool _isCorrect = false;
    [ObservableProperty] private string _explanation = string.Empty;
    [ObservableProperty] private int _questionProgress = 0;
    [ObservableProperty] private int _questionTotal = 0;
    [ObservableProperty] private bool _isLoadingExplanation = false;
    [ObservableProperty] private bool _hasQuestion = false;

    [ObservableProperty] private string _aiProviderName = string.Empty;
    [ObservableProperty] private int _lastTokenCount = 0;
    [ObservableProperty] private string _lastCostText = string.Empty;

    public double SessionProgress => QuestionTotal > 0 ? (double)QuestionProgress / QuestionTotal : 0;
    public string SessionProgressText => $"{QuestionProgress} / {QuestionTotal}";
    public bool IsEmpty => !HasQuestion;

    partial void OnHasQuestionChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnQuestionProgressChanged(int value) => OnPropertyChanged(nameof(SessionProgressText));
    partial void OnQuestionTotalChanged(int value) => OnPropertyChanged(nameof(SessionProgressText));

    private async Task LoadQueueAsync()
    {
        var rows = await _db.Questions
            .Include(q => q.Subject)
            .OrderBy(q => q.SuccessRate)
            .ThenBy(q => q.TimesAnswered)
            .Take(20)
            .ToListAsync();

        _queue.Clear();
        foreach (var q in rows) _queue.Enqueue(q);
        QuestionTotal = _queue.Count;
        QuestionProgress = 0;
        LoadNextFromQueue();
    }

    private void LoadNextFromQueue()
    {
        if (_queue.Count == 0)
        {
            HasQuestion = false;
            QuestionText = string.Empty;
            Options = new ObservableCollection<AnswerOption>();
            return;
        }

        _current = _queue.Dequeue();
        QuestionText  = _current.Text;
        SubjectName   = _current.Subject?.Name ?? "—";
        SubjectColor  = _current.Subject?.Color ?? "#5B8CFF";
        QuestionTag   = _current.Tags.FirstOrDefault() ?? string.Empty;
        DifficultyTag = _current.Difficulty.ToString();
        QuestionNumber = $"#{(QuestionProgress + 1):D3}";

        var labels = new[] { "A", "B", "C", "D" };
        Options = new ObservableCollection<AnswerOption>(_current.Options.Take(4).Select((opt, i) => new AnswerOption
        {
            Label = labels[i],
            Text  = opt,
        }));
        HasQuestion = true;
    }

    [RelayCommand]
    private void SelectOption(AnswerOption option)
    {
        if (IsAnswered) return;
        SelectedOption = option;
        foreach (var o in Options) o.IsSelected = o == option;
    }

    [RelayCommand]
    private async Task SubmitAnswerAsync()
    {
        if (SelectedOption == null || IsAnswered || _current == null) return;
        IsAnswered = true;

        // Compare against the question's CorrectAnswer string
        IsCorrect = string.Equals(SelectedOption.Text.Trim(), _current.CorrectAnswer.Trim(),
                                  StringComparison.OrdinalIgnoreCase);

        foreach (var o in Options)
        {
            o.IsCorrect = string.Equals(o.Text.Trim(), _current.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
            o.IsWrong = o == SelectedOption && !IsCorrect;
        }

        // Persist answer + update progress
        await _questions.SaveAnswerAsync(new Answer
        {
            QuestionId = _current.Id,
            UserAnswer = SelectedOption.Text,
            IsCorrect  = IsCorrect,
            Timestamp  = DateTime.UtcNow,
        });
        if (IsCorrect) await _progress.RecordCorrectAnswerAsync(_current.SubjectId);
        else           await _progress.RecordWrongAnswerAsync(_current.SubjectId);

        // If the DB carries an explanation, show it directly; otherwise the user clicks the AI button
        if (!string.IsNullOrWhiteSpace(_current.Explanation))
            Explanation = _current.Explanation;
    }

    [RelayCommand]
    private async Task ShowAIExplanationAsync()
    {
        if (_current == null) return;
        IsLoadingExplanation = true;

        var response = await _aiSelector.ExecuteAsync(TaskType.QAExplanation, _current.Text);
        if (response.IsSuccess)
        {
            Explanation     = response.Content;
            AiProviderName  = response.ProviderName;
            LastTokenCount  = response.TokensUsed;
            LastCostText    = CostCalculator.FormatCost(response.CostUSD);
        }
        else
        {
            Explanation     = $"KI-Fehler: {response.Error}";
            AiProviderName  = response.ProviderName;
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
        Explanation = string.Empty;
        AiProviderName = string.Empty;
        LastTokenCount = 0;
        LastCostText = string.Empty;
        LoadNextFromQueue();
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
