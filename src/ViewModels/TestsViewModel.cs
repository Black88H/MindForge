using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Services;
using System.Collections.ObjectModel;

namespace MindForge.ViewModels;

public partial class TestsViewModel : ObservableObject
{
    private readonly TestRunnerService? _runner;
    private TestSession? _activeSession;
    private System.Timers.Timer? _timer;

    public TestsViewModel() : this(null) { }
    public TestsViewModel(TestRunnerService? runner) => _runner = runner;

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

    // History
    [ObservableProperty] private ObservableCollection<TestHistoryItem> _testHistory = new()
    {
        new() { Name="Analysis Klausur 1", Subject="Analysis II", Score=87, Date="23.04.2026", Questions=20 },
        new() { Name="Quantum Quiz",       Subject="Quantenmechanik", Score=72, Date="21.04.2026", Questions=15 },
        new() { Name="English C1 Mock",    Subject="Englisch C1", Score=95, Date="19.04.2026", Questions=30 },
    };

    // Prüfungsvorbereitung
    [ObservableProperty] private string _examSubject = string.Empty;
    [ObservableProperty] private DateTime _examDate = DateTime.Today.AddDays(7);
    public int DaysUntilExam => Math.Max(0, (ExamDate - DateTime.Today).Days);
    public string ExamCountdown => DaysUntilExam == 0 ? "Heute!" : $"in {DaysUntilExam} Tagen";

    public string QuestionProgress => IsTestRunning
        ? $"Frage {CurrentQuestionIndex + 1} von {TotalQuestions}" : string.Empty;
    public double TestProgress => TotalQuestions > 0
        ? (double)CurrentQuestionIndex / TotalQuestions : 0;

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

        LoadMockQuestion(0);

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
        CurrentIsCorrect = SelectedOption.Label == "A"; // placeholder

        foreach (var o in CurrentOptions)
        {
            o.IsCorrect = o.Label == "A";
            o.IsWrong = o == SelectedOption && !CurrentIsCorrect;
        }
        if (CurrentIsCorrect) FinalCorrect++;
    }

    [RelayCommand]
    private void NextTestQuestion()
    {
        if (CurrentQuestionIndex >= TotalQuestions - 1)
        {
            EndTestCommand.Execute(null);
            return;
        }
        CurrentQuestionIndex++;
        CurrentAnswered = false;
        SelectedOption = null;
        LoadMockQuestion(CurrentQuestionIndex);
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
        ErrorAnalysis = FinalCorrect < FinalTotal ? "Fehler in: Theorie, Berechnungen" : "Perfekt!";
        SmartSuggestions = new(["Wiederhole Kapitel 3", "Übe Berechnungsaufgaben"]);

        TestHistory.Insert(0, new TestHistoryItem
        {
            Name = string.IsNullOrEmpty(TestName) ? "Unbenannter Test" : TestName,
            Subject = SelectedSubject,
            Score = (int)FinalScore,
            Date = DateTime.Today.ToString("dd.MM.yyyy"),
            Questions = FinalTotal
        });
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
            OcrStatus = "Verarbeitung...";
            OcrProgress = 0;
            SimulateOCRProgress();
        }
    }

    private void LoadMockQuestion(int index)
    {
        CurrentQuestionText = $"Frage {index + 1}: Was ist das Ergebnis von 2 + 2?";
        CurrentOptions = new ObservableCollection<AnswerOption>
        {
            new() { Label = "A", Text = "4" },
            new() { Label = "B", Text = "3" },
            new() { Label = "C", Text = "5" },
            new() { Label = "D", Text = "22" },
        };
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

    private async void SimulateOCRProgress()
    {
        for (int i = 0; i <= 100; i += 10)
        {
            await Task.Delay(200);
            OcrProgress = i;
            OcrStatus = i < 100 ? $"Verarbeitung... {i}%" : "✓ OCR abgeschlossen";
        }
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
}
