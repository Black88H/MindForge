using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;

namespace MindForge.ViewModels;

public partial class TestCreatorViewModel : ObservableObject
{
    private readonly MindForgeDbContext _db;

    public TestCreatorViewModel(MindForgeDbContext db)
    {
        _db = db;
        _ = LoadQuestionsAsync();
    }

    [ObservableProperty] private string _testType = "Quiz";
    [ObservableProperty] private string _selectedDifficulty = "Gemischt";
    [ObservableProperty] private int _durationMinutes = 30;
    [ObservableProperty] private string _testName = string.Empty;
    [ObservableProperty] private bool _isRunning = false;
    [ObservableProperty] private int _timeRemainingSeconds = 0;
    [ObservableProperty] private int _currentQuestionIndex = 0;
    [ObservableProperty] private bool _isSubmitted = false;
    [ObservableProperty] private double _score = 0;
    [ObservableProperty] private bool _isLoading = false;

    [ObservableProperty] private List<TestQuestionItem> _availableQuestions = new();

    public List<string> TestTypes { get; } = ["Quiz", "Prüfungssimulation", "Schwachstellentraining"];
    public List<string> Difficulties { get; } = ["Leicht", "Mittel", "Schwer", "Gemischt"];
    public int SelectedCount => AvailableQuestions.Count(q => q.IsSelected);
    public string DurationText => $"{DurationMinutes} Minuten";
    public string TimeRemainingText => $"{TimeRemainingSeconds / 60:D2}:{TimeRemainingSeconds % 60:D2}";
    public bool IsEmpty => !IsLoading && AvailableQuestions.Count == 0;

    partial void OnAvailableQuestionsChanged(List<TestQuestionItem> value)
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(IsEmpty));
    }
    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnDurationMinutesChanged(int value) => OnPropertyChanged(nameof(DurationText));
    partial void OnTimeRemainingSecondsChanged(int value) => OnPropertyChanged(nameof(TimeRemainingText));

    private async Task LoadQuestionsAsync()
    {
        IsLoading = true;
        try
        {
            var rows = await _db.Questions
                .Include(q => q.Subject)
                .OrderBy(q => q.Subject!.Name)
                .ThenBy(q => q.CreatedAt)
                .Take(200)
                .ToListAsync();

            AvailableQuestions = rows.Select(q => new TestQuestionItem
            {
                Id = q.Id,
                Text = q.Text,
                Subject = q.Subject?.Name ?? "—",
                IsSelected = false,
            }).ToList();
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task StartTestAsync()
    {
        if (SelectedCount == 0) return;
        IsRunning = true;
        TimeRemainingSeconds = DurationMinutes * 60;
        CurrentQuestionIndex = 0;
        while (TimeRemainingSeconds > 0 && IsRunning && !IsSubmitted)
        {
            await Task.Delay(1000);
            TimeRemainingSeconds--;
        }
        if (!IsSubmitted) await SubmitTestAsync();
    }

    [RelayCommand]
    private async Task SubmitTestAsync()
    {
        IsRunning = false;
        IsSubmitted = true;
        // Real scoring happens in Phase 1; for now record an empty result so the UI flow works
        Score = 0;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ResetTest()
    {
        IsRunning = false;
        IsSubmitted = false;
        TimeRemainingSeconds = 0;
        CurrentQuestionIndex = 0;
        Score = 0;
    }

    [RelayCommand]
    private void ToggleQuestion(TestQuestionItem question)
    {
        question.IsSelected = !question.IsSelected;
        OnPropertyChanged(nameof(SelectedCount));
    }
}

public partial class TestQuestionItem : ObservableObject
{
    public Guid Id { get; set; } = Guid.Empty;
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private bool _isSelected = false;
}
