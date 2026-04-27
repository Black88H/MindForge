using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;

namespace MindForge.ViewModels;

public partial class TestsViewModel : ObservableObject
{
    private readonly TestRunnerService _runner;
    private readonly MindForgeDbContext _db;
    private System.Timers.Timer? _timer;

    public TestsViewModel(TestRunnerService runner, MindForgeDbContext db)
    {
        _runner = runner;
        _db = db;
        _ = LoadHistoryAsync();
    }

    // Creator tabs
    [ObservableProperty] private string _activeCreatorTab = "Neu";
    public List<string> CreatorTabs { get; } = ["Neu", "Aus Lernplan", "Aus Vorlage", "OCR Upload"];

    // Creator form
    [ObservableProperty] private string _testName = string.Empty;
    [ObservableProperty] private string _selectedSubject = "Alle Fächer";
    [ObservableProperty] private string _selectedDifficulty = "Mittel";
    [ObservableProperty] private int _questionCount = 20;
    [ObservableProperty] private string _selectedMode = "Normal";
    public List<string> TestModes { get; } = ["Normal", "Prüfung", "Üben", "Custom"];
    public List<string> Difficulties { get; } = ["Leicht", "Mittel", "Schwer", "Gemischt"];

    // Test settings
    [ObservableProperty] private bool _timerEnabled = false;
    [ObservableProperty] private int _timerMinutes = 30;
    [ObservableProperty] private bool _showProgressBar = true;
    [ObservableProperty] private bool _showCorrectAnswer = true;
    [ObservableProperty] private bool _hintsEnabled = false;
    [ObservableProperty] private bool _liveFeedbackEnabled = false;

    // Test runner state
    [ObservableProperty] private bool _isTestRunning = false;
    [ObservableProperty] private bool _isTestFinished = false;
    [ObservableProperty] private int _currentQuestionIndex = 0;
    [ObservableProperty] private int _totalQuestions = 0;
    [ObservableProperty] private string _currentQuestionText = string.Empty;
    [ObservableProperty] private ObservableCollection<AnswerOption> _currentOptions = new();
    [ObservableProperty] private AnswerOption? _selectedOption;
    [ObservableProperty] private bool _currentAnswered = false;
    [ObservableProperty] private bool _currentIsCorrect = false;
    [ObservableProperty] private string _currentExplanation = string.Empty;

    // Timer
    [ObservableProperty] private int _timeRemainingSeconds = 1800;
    [ObservableProperty] private bool _timerCritical = false;
    public string TimeRemainingText => $"{TimeRemainingSeconds / 60:D2}:{TimeRemainingSeconds % 60:D2}";

    // Results
    [ObservableProperty] private double _finalScore = 0;
    [ObservableProperty] private int _finalCorrect = 0;
    [ObservableProperty] private int _finalTotal = 0;
    [ObservableProperty] private string _finalTime = string.Empty;
    [ObservableProperty] private int _xpEarned = 0;
    [ObservableProperty] private string _errorAnalysis = string.Empty;
    [ObservableProperty] private ObservableCollection<string> _smartSuggestions = new();

    // OCR upload
    [ObservableProperty] private string _ocrUploadPath = string.Empty;
    [ObservableProperty] private string _ocrStatus = string.Empty;
    [ObservableProperty] private int _ocrProgress = 0;

    // History — populated from UserTestHistory
    [ObservableProperty] private ObservableCollection<TestHistoryItem> _testHistory = new();

    // Prüfungsvorbereitung
    [ObservableProperty] private string _examSubject = string.Empty;
    [ObservableProperty] private DateTime _examDate = DateTime.Today.AddDays(7);
    public int DaysUntilExam => Math.Max(0, (ExamDate - DateTime.Today).Days);
    public string ExamCountdown => DaysUntilExam == 0 ? "Heute!" : $"in {DaysUntilExam} Tagen";

    public string QuestionProgress => IsTestRunning
        ? $"Frage {CurrentQuestionIndex + 1} von {TotalQuestions}" : string.Empty;
    public double TestProgress => TotalQuestions > 0
        ? Math.Clamp((double)CurrentQuestionIndex / TotalQuestions, 0, 1) : 0;
    public bool IsLastQuestion => TotalQuestions > 0 && CurrentQuestionIndex >= TotalQuestions - 1;
    public bool HasNoHistory => TestHistory.Count == 0;

    private async Task LoadHistoryAsync()
    {
        var rows = await _db.UserTestHistory
            .Include(h => h.Test)
            .OrderByDescending(h => h.LastAttempt)
            .Take(20)
            .ToListAsync();

        TestHistory = new ObservableCollection<TestHistoryItem>(rows.Select(h => new TestHistoryItem
        {
            Name      = h.Test?.Name ?? "Unbenannter Test",
            Subject   = h.Test?.Subject?.Name ?? "—",
            Score     = (int)Math.Round(h.Score),
            Date      = h.LastAttempt.ToString("dd.MM.yyyy"),
            Questions = h.TotalQuestions,
        }));
        OnPropertyChanged(nameof(HasNoHistory));
    }

    [RelayCommand]
    private void SetCreatorTab(string tab) => ActiveCreatorTab = tab;

    [RelayCommand]
    private async Task StartTestAsync()
    {
        IsTestRunning = true;
        IsTestFinished = false;
        CurrentQuestionIndex = 0;
        TotalQuestions = QuestionCount;
        TimeRemainingSeconds = TimerEnabled ? TimerMinutes * 60 : 0;

        await LoadQuestionAsync(0);

        if (TimerEnabled)
            StartTimer();
    }

    [RelayCommand]
    private void SelectTestOption(AnswerOption opt)
    {
        if (CurrentAnswered) return;
        SelectedOption = opt;
        foreach (var o in CurrentOptions) o.IsSelected = o == opt;
    }

    [RelayCommand]
    private async Task SubmitTestAnswerAsync()
    {
        if (SelectedOption == null || CurrentAnswered) return;
        CurrentAnswered = true;
        CurrentIsCorrect = SelectedOption.Label == "A"; // placeholder until real test engine
        await Task.CompletedTask;

        foreach (var o in CurrentOptions)
        {
            o.IsCorrect = o.Label == "A";
            o.IsWrong = o == SelectedOption && !CurrentIsCorrect;
        }
        if (CurrentIsCorrect) FinalCorrect++;
    }

    [RelayCommand]
    private async Task NextTestQuestionAsync()
    {
        if (CurrentQuestionIndex >= TotalQuestions - 1)
        {
            EndTestCommand.Execute(null);
            return;
        }
        CurrentQuestionIndex++;
        OnPropertyChanged(nameof(IsLastQuestion));
        CurrentAnswered = false;
        SelectedOption = null;
        await LoadQuestionAsync(CurrentQuestionIndex);
    }

    [RelayCommand]
    private void EndTest()
    {
        _timer?.Stop();
        IsTestRunning = false;
        IsTestFinished = true;
        FinalTotal = TotalQuestions;
        FinalScore = FinalTotal > 0 ? Math.Round((double)FinalCorrect / FinalTotal * 100, 1) : 0;
        FinalTime = $"{(TimerMinutes * 60 - TimeRemainingSeconds) / 60:D2}:{(TimerMinutes * 60 - TimeRemainingSeconds) % 60:D2}";
        XpEarned = (int)(FinalScore * 2);
        ErrorAnalysis = FinalCorrect < FinalTotal ? "Schwächen identifiziert" : "Perfekter Test!";
        SmartSuggestions = new(["Wiederhole falsch beantwortete Themen", "Erstelle Karteikarten zu Schwachstellen"]);

        TestHistory.Insert(0, new TestHistoryItem
        {
            Name = string.IsNullOrEmpty(TestName) ? "Unbenannter Test" : TestName,
            Subject = SelectedSubject,
            Score = (int)FinalScore,
            Date = DateTime.Today.ToString("dd.MM.yyyy"),
            Questions = FinalTotal
        });
        OnPropertyChanged(nameof(HasNoHistory));
    }

    [RelayCommand]
    private void ResetTest()
    {
        IsTestRunning = false;
        IsTestFinished = false;
        CurrentQuestionIndex = 0;
        FinalCorrect = 0;
    }

    [RelayCommand]
    private void UploadOCR()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Bilder & PDFs|*.pdf;*.png;*.jpg;*.jpeg;*.bmp"
        };
        if (dialog.ShowDialog() == true)
        {
            OcrUploadPath = dialog.FileName;
            OcrStatus = "OCR wird in einer späteren Phase implementiert.";
            OcrProgress = 0;
        }
    }

    private async Task LoadQuestionAsync(int index)
    {
        // Pull a real question from the selected subject if available; fall back to placeholder
        Question? q = null;
        var subjects = await _db.Subjects
            .Where(s => SelectedSubject == "Alle Fächer" || s.Name == SelectedSubject)
            .ToListAsync();
        if (subjects.Count > 0)
        {
            var subjectIds = subjects.Select(s => s.Id).ToList();
            q = await _db.Questions
                .Where(x => subjectIds.Contains(x.SubjectId))
                .OrderBy(x => x.SuccessRate)
                .ThenBy(x => x.TimesAnswered)
                .Skip(index)
                .FirstOrDefaultAsync();
        }

        if (q != null)
        {
            CurrentQuestionText = q.Text;
            var labels = new[] { "A", "B", "C", "D" };
            CurrentOptions = new ObservableCollection<AnswerOption>(
                q.Options.Take(4).Select((opt, i) => new AnswerOption
                {
                    Label = labels[i],
                    Text  = opt,
                }));
        }
        else
        {
            CurrentQuestionText = $"Frage {index + 1}: (keine Fragen für dieses Fach in der Datenbank — Phase 1 generiert sie)";
            CurrentOptions = new ObservableCollection<AnswerOption>
            {
                new() { Label = "A", Text = "—" },
                new() { Label = "B", Text = "—" },
                new() { Label = "C", Text = "—" },
                new() { Label = "D", Text = "—" },
            };
        }
        CurrentExplanation = string.Empty;
    }

    private void StartTimer()
    {
        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += (_, _) =>
        {
            if (TimeRemainingSeconds > 0)
            {
                TimeRemainingSeconds--;
                TimerCritical = TimeRemainingSeconds <= 300;
                OnPropertyChanged(nameof(TimeRemainingText));
            }
            else
            {
                _timer.Stop();
                System.Windows.Application.Current.Dispatcher.Invoke(() => EndTestCommand.Execute(null));
            }
        };
        _timer.Start();
    }
}

public class TestHistoryItem
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int Score { get; set; }
    public string Date { get; set; } = string.Empty;
    public int Questions { get; set; }
    public string ScoreText => $"{Score}%";
    public string ScoreColor => Score >= 80 ? "#3FCF8E" : Score >= 60 ? "#FFB547" : "#FF6B6B";
    public string QuestionsText => $"{Questions} Fragen";
    public string ModeText => "Normal";
}
