using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class LearningViewModel : ObservableObject
{
    private readonly IAISelector _ai;
    private readonly SpacedRepetitionService _sr;
    private readonly GamificationService _gamification;
    private readonly QuestionRepository _questions;
    private readonly UserProgressRepository _progress;
    private readonly MindForgeDbContext _db;

    private readonly Queue<Question> _queue = new();
    private Question? _current;

    public LearningViewModel(
        IAISelector ai,
        SpacedRepetitionService sr,
        GamificationService gamification,
        QuestionRepository questions,
        UserProgressRepository progress,
        MindForgeDbContext db)
    {
        _ai = ai;
        _sr = sr;
        _gamification = gamification;
        _questions = questions;
        _progress = progress;
        _db = db;
        _ = LoadAsync();
    }

    // Subject & Filter
    [ObservableProperty] private string _selectedSubject = "Alle Fächer";
    [ObservableProperty] private string _selectedDifficulty = "Alle";
    public List<string> DifficultyOptions { get; } = ["Alle", "Leicht", "Mittel", "Schwer"];

    // Current Question
    [ObservableProperty] private string _subjectName = string.Empty;
    [ObservableProperty] private string _subjectColor = "#5B8CFF";
    [ObservableProperty] private string _questionTag = string.Empty;
    [ObservableProperty] private string _difficultyTag = string.Empty;
    [ObservableProperty] private string _questionNumber = string.Empty;
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private bool _hasQuestion = false;
    [ObservableProperty] private ObservableCollection<AnswerOption> _options = new();

    [ObservableProperty] private AnswerOption? _selectedOption;
    [ObservableProperty] private bool _isAnswered = false;
    [ObservableProperty] private bool _isCorrect = false;
    [ObservableProperty] private bool _showExplanation = false;
    [ObservableProperty] private bool _isLoadingExplanation = false;
    [ObservableProperty] private string _explanation = string.Empty;

    // Session stats
    [ObservableProperty] private int _questionProgress = 0;
    [ObservableProperty] private int _questionTotal = 0;
    [ObservableProperty] private int _correctToday = 0;
    [ObservableProperty] private int _totalToday = 0;
    [ObservableProperty] private int _streakDays = UserSession.CurrentStreak;
    [ObservableProperty] private bool _streakInDanger = false;
    [ObservableProperty] private int _currentXP = UserSession.TotalXP;
    [ObservableProperty] private int _xpToNextLevel = 100;
    [ObservableProperty] private int _level = UserSession.Level;
    [ObservableProperty] private DateTime _nextReviewDate = DateTime.Today.AddDays(1);

    // AI info
    [ObservableProperty] private string _aiProviderName = string.Empty;
    [ObservableProperty] private string _lastCostText = string.Empty;

    // Learning plan panel
    [ObservableProperty] private bool _showPlanPanel = false;
    [ObservableProperty] private int _planStep = 1;
    [ObservableProperty] private string _planTitle = string.Empty;
    [ObservableProperty] private int _planDays = 14;
    [ObservableProperty] private int _planMinutesPerDay = 60;
    [ObservableProperty] private ObservableCollection<LearningMethodItem> _availableMethods = new();

    // Material library
    [ObservableProperty] private ObservableCollection<MaterialItem> _materials = new();
    [ObservableProperty] private string _materialSearchText = string.Empty;
    [ObservableProperty] private string _activeMaterialTab = "PDFs";
    public List<string> MaterialTabs { get; } = ["PDFs", "Links", "Notizen"];

    // Layout
    [ObservableProperty] private string _layoutMode = "Default";

    public double SessionProgress => QuestionTotal > 0 ? (double)QuestionProgress / QuestionTotal : 0;
    public string SessionProgressText => $"{QuestionProgress} / {QuestionTotal}";
    public string TodayStatsText => $"{CorrectToday}/{TotalToday}";
    public double XPProgress => XpToNextLevel > 0 ? Math.Clamp((double)CurrentXP / XpToNextLevel, 0, 1) : 0;
    public bool IsEmpty => !HasQuestion;

    partial void OnHasQuestionChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnQuestionProgressChanged(int value) => OnPropertyChanged(nameof(SessionProgressText));
    partial void OnQuestionTotalChanged(int value) => OnPropertyChanged(nameof(SessionProgressText));
    partial void OnCorrectTodayChanged(int value) => OnPropertyChanged(nameof(TodayStatsText));
    partial void OnTotalTodayChanged(int value) => OnPropertyChanged(nameof(TodayStatsText));

    private async Task LoadAsync()
    {
        // XP budget for next level — match GamificationService.GetXPForNextLevel
        XpToNextLevel = (int)Math.Pow(Math.Max(1, Level), 2) * 50;

        // Available methods from DB
        var methods = await _db.LearningMethods.OrderBy(m => m.Type).ToListAsync();
        AvailableMethods = new ObservableCollection<LearningMethodItem>(methods.Select(m => new LearningMethodItem
        {
            Name = m.Name,
            Icon = m.Icon,
            IsSelected = m.Type is LearningMethodType.ActiveRecall or LearningMethodType.SpacedRepetition,
        }));

        // Today's progress (global, default user)
        var global = await _progress.GetGlobalProgressAsync();
        CorrectToday = global.CorrectToday;
        TotalToday   = global.TotalToday;
        StreakDays   = global.CurrentStreak;
        StreakInDanger = global.LastStreakDate.Date < DateTime.UtcNow.Date.AddDays(-1);

        // Question queue: prefer due reviews, fall back to weakest questions
        var due = await _db.SpacedRepetitionItems
            .Include(s => s.UserProgress)
            .Where(s => s.NextReviewDate <= DateTime.UtcNow.Date)
            .OrderBy(s => s.NextReviewDate)
            .Take(10)
            .ToListAsync();
        // SpacedRepetitionItem references UserProgress (subject), not directly Question — fall back to weakest
        var rows = due.Count > 0
            ? new List<Question>()
            : await _db.Questions
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
        Options = new ObservableCollection<AnswerOption>(
            _current.Options.Take(4).Select((opt, i) => new AnswerOption { Label = labels[i], Text = opt }));
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
        IsCorrect = string.Equals(SelectedOption.Text.Trim(), _current.CorrectAnswer.Trim(),
                                  StringComparison.OrdinalIgnoreCase);

        foreach (var o in Options)
        {
            o.IsCorrect = string.Equals(o.Text.Trim(), _current.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
            o.IsWrong = o == SelectedOption && !IsCorrect;
        }

        QuestionProgress++;
        TotalToday++;
        if (IsCorrect) CorrectToday++;

        await _questions.SaveAnswerAsync(new Answer
        {
            QuestionId = _current.Id,
            UserAnswer = SelectedOption.Text,
            IsCorrect  = IsCorrect,
        });

        if (IsCorrect)
        {
            await _progress.RecordCorrectAnswerAsync(_current.SubjectId);
            var xp = await _gamification.AddXPAsync("default", XPAction.CorrectAnswer);
            CurrentXP += xp;
        }
        else
        {
            await _progress.RecordWrongAnswerAsync(_current.SubjectId);
        }
    }

    [RelayCommand]
    private async Task ShowAIExplanationAsync()
    {
        if (_current == null) return;
        IsLoadingExplanation = true;
        var resp = await _ai.ExecuteAsync(TaskType.QAExplanation, _current.Text);
        if (resp.IsSuccess)
        {
            Explanation = resp.Content;
            AiProviderName = resp.ProviderName;
            LastCostText = CostCalculator.FormatCost(resp.CostUSD);
        }
        else
        {
            Explanation = $"KI-Fehler: {resp.Error}";
        }
        IsLoadingExplanation = false;
        ShowExplanation = true;
    }

    [RelayCommand]
    private void NextQuestion()
    {
        IsAnswered = false;
        SelectedOption = null;
        ShowExplanation = false;
        Explanation = string.Empty;
        AiProviderName = string.Empty;
        LoadNextFromQueue();
    }

    [RelayCommand]
    private void TogglePlanPanel() => ShowPlanPanel = !ShowPlanPanel;

    [RelayCommand]
    private void NextPlanStep() { if (PlanStep < 5) PlanStep++; }

    [RelayCommand]
    private void PrevPlanStep() { if (PlanStep > 1) PlanStep--; }

    [RelayCommand]
    private void ToggleMethod(LearningMethodItem m) => m.IsSelected = !m.IsSelected;

    [RelayCommand]
    private void UploadMaterial()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Dokumente|*.pdf;*.docx;*.txt;*.png;*.jpg|Alle|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            Materials.Add(new MaterialItem
            {
                FileName = System.IO.Path.GetFileName(dialog.FileName),
                FilePath = dialog.FileName,
                FileType = dialog.FileName.EndsWith(".pdf") ? "PDF" : "Dokument"
            });
        }
    }

    [RelayCommand]
    private void SetMaterialTab(string tab) => ActiveMaterialTab = tab;
}

public partial class LearningMethodItem : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _icon = "📚";
    [ObservableProperty] private bool _isSelected = false;
}

public partial class MaterialItem : ObservableObject
{
    [ObservableProperty] private string _fileName = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _fileType = "PDF";
    [ObservableProperty] private DateTime _uploadedDate = DateTime.Now;
}
