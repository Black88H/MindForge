using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Services;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using MindForge.Services.AI.Utilities;
using System.Collections.ObjectModel;

namespace MindForge.ViewModels;

public partial class LearningViewModel : ObservableObject
{
    private readonly IAISelector? _ai;
    private readonly SpacedRepetitionService? _sr;
    private readonly GamificationService? _gamification;

    public LearningViewModel() : this(null, null, null) { }
    public LearningViewModel(IAISelector? ai, SpacedRepetitionService? sr, GamificationService? gamification)
    {
        _ai = ai;
        _sr = sr;
        _gamification = gamification;
    }

    // Subject & Filter
    [ObservableProperty] private string _selectedSubject = "Alle Fächer";
    [ObservableProperty] private string _selectedDifficulty = "Alle";
    public List<string> DifficultyOptions { get; } = ["Alle", "Leicht", "Mittel", "Schwer"];

    // Current Question
    [ObservableProperty] private string _subjectName = "Analysis II";
    [ObservableProperty] private string _subjectColor = "#5B8CFF";
    [ObservableProperty] private string _questionTag = "Reihen";
    [ObservableProperty] private string _difficultyTag = "Mittel";
    [ObservableProperty] private string _questionNumber = "#047";
    [ObservableProperty] private string _questionText = "Welche Reihe konvergiert nach dem Leibniz-Kriterium, aber nicht absolut?";
    [ObservableProperty] private ObservableCollection<AnswerOption> _options = new()
    {
        new() { Label = "A", Text = "∑ 1/n²" },
        new() { Label = "B", Text = "∑ (-1)ⁿ / n" },
        new() { Label = "C", Text = "∑ (-1)ⁿ / n²" },
        new() { Label = "D", Text = "∑ 1/√n" },
    };

    [ObservableProperty] private AnswerOption? _selectedOption;
    [ObservableProperty] private bool _isAnswered = false;
    [ObservableProperty] private bool _isCorrect = false;
    [ObservableProperty] private bool _showExplanation = false;
    [ObservableProperty] private bool _isLoadingExplanation = false;
    [ObservableProperty] private string _explanation = string.Empty;

    // Session stats
    [ObservableProperty] private int _questionProgress = 0;
    [ObservableProperty] private int _questionTotal = 20;
    [ObservableProperty] private int _correctToday = 0;
    [ObservableProperty] private int _totalToday = 0;
    [ObservableProperty] private int _streakDays = 0;
    [ObservableProperty] private bool _streakInDanger = false;
    [ObservableProperty] private int _currentXP = 0;
    [ObservableProperty] private int _xpToNextLevel = 500;
    [ObservableProperty] private int _level = 1;
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
    [ObservableProperty] private ObservableCollection<LearningMethodItem> _availableMethods = new()
    {
        new() { Name = "Active Recall",      Icon = "🧠", IsSelected = true },
        new() { Name = "Spaced Repetition",  Icon = "🔄", IsSelected = true },
        new() { Name = "Pomodoro",           Icon = "🍅", IsSelected = false },
        new() { Name = "Interleaving",       Icon = "🔀", IsSelected = false },
        new() { Name = "Practice Test",      Icon = "📝", IsSelected = false },
    };

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
        if (SelectedOption == null || IsAnswered) return;
        IsAnswered = true;
        IsCorrect = SelectedOption.Label == "B";
        QuestionProgress++;
        TotalToday++;
        if (IsCorrect) CorrectToday++;

        foreach (var o in Options)
        {
            o.IsCorrect = o.Label == "B";
            o.IsWrong = o == SelectedOption && !IsCorrect;
        }

        if (_gamification != null && IsCorrect)
        {
            var xp = await _gamification.AddXPAsync("default", XPAction.CorrectAnswer);
            CurrentXP += xp;
        }
    }

    [RelayCommand]
    private async Task ShowAIExplanationAsync()
    {
        IsLoadingExplanation = true;
        if (_ai != null)
        {
            var resp = await _ai.ExecuteAsync(TaskType.QAExplanation, QuestionText);
            if (resp.IsSuccess)
            {
                Explanation = resp.Content;
                AiProviderName = resp.ProviderName;
                LastCostText = CostCalculator.FormatCost(resp.CostUSD);
            }
            else Explanation = $"KI-Fehler: {resp.Error}";
        }
        else
        {
            await Task.Delay(500);
            Explanation = "Die Reihe ∑ (-1)ⁿ / n konvergiert bedingt nach dem Leibniz-Kriterium.";
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
        AiProviderName = string.Empty;
        foreach (var o in Options) { o.IsSelected = false; o.IsCorrect = false; o.IsWrong = false; }
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
