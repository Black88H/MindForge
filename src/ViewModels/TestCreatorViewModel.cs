using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MindForge.ViewModels;

public partial class TestCreatorViewModel : ObservableObject
{
    [ObservableProperty] private string _testType = "Quiz";
    [ObservableProperty] private string _selectedDifficulty = "Gemischt";
    [ObservableProperty] private int _durationMinutes = 30;
    [ObservableProperty] private string _testName = string.Empty;
    [ObservableProperty] private bool _isRunning = false;
    [ObservableProperty] private int _timeRemainingSeconds = 0;
    [ObservableProperty] private int _currentQuestionIndex = 0;
    [ObservableProperty] private bool _isSubmitted = false;
    [ObservableProperty] private double _score = 0;

    [ObservableProperty] private List<TestQuestionItem> _availableQuestions = new()
    {
        new() { Text="Leibniz-Kriterium",    Subject="Analysis II",  IsSelected=true  },
        new() { Text="Taylor-Reihen",         Subject="Analysis II",  IsSelected=true  },
        new() { Text="Fourier-Transformation",Subject="Analysis II",  IsSelected=false },
        new() { Text="Quantenverschränkung",  Subject="Quantenmechanik", IsSelected=false },
        new() { Text="Schrödinger-Gleichung", Subject="Quantenmechanik", IsSelected=true  },
        new() { Text="Present Perfect",       Subject="English C1",   IsSelected=false },
        new() { Text="Conditionals",          Subject="English C1",   IsSelected=true  },
        new() { Text="Dijkstra-Algorithmus",  Subject="Algorithmen",  IsSelected=true  },
        new() { Text="Big-O Notation",        Subject="Algorithmen",  IsSelected=false },
    };

    public List<string> TestTypes { get; } = ["Quiz", "Prüfungssimulation", "Schwachstellentraining"];
    public List<string> Difficulties { get; } = ["Leicht", "Mittel", "Schwer", "Gemischt"];
    public int SelectedCount => AvailableQuestions.Count(q => q.IsSelected);
    public string DurationText => $"{DurationMinutes} Minuten";
    public string TimeRemainingText => $"{TimeRemainingSeconds / 60:D2}:{TimeRemainingSeconds % 60:D2}";

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
        Score = 78.5;
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
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private bool _isSelected = false;
}
