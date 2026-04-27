using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SubjectRepository _subjectRepo;

    public MainViewModel(SubjectRepository subjectRepo)
    {
        _subjectRepo = subjectRepo;
        _userName    = UserSession.Username;
        _level       = UserSession.Level;
        _currentXP   = UserSession.TotalXP;
        _xpToNextLevel = (int)Math.Pow(Math.Max(1, UserSession.Level), 2) * 50;
        _streak      = UserSession.CurrentStreak;
        _ = LoadSubjectsAsync();
    }

    [ObservableProperty] private string _currentView = "Dashboard";
    [ObservableProperty] private bool _isSidebarCollapsed = false;
    [ObservableProperty] private string _userName;
    [ObservableProperty] private int _level;
    [ObservableProperty] private int _currentXP;
    [ObservableProperty] private int _xpToNextLevel;
    [ObservableProperty] private int _streak;
    [ObservableProperty] private string _syncStatus = "Synchronisiert";
    [ObservableProperty] private bool _isSyncing = false;
    [ObservableProperty] private string _breadcrumb = "Dashboard";

    [ObservableProperty] private ObservableCollection<SubjectViewModel> _subjects = new();

    [ObservableProperty] private SubjectViewModel? _activeSubject;

    public double XPProgress => XpToNextLevel > 0 ? (double)CurrentXP / XpToNextLevel : 0;
    public string XPProgressText => $"{CurrentXP} / {XpToNextLevel} XP";
    public bool HasSubjects => Subjects.Count > 0;

    partial void OnCurrentXPChanged(int value) => OnPropertyChanged(nameof(XPProgressText));
    partial void OnXpToNextLevelChanged(int value) => OnPropertyChanged(nameof(XPProgressText));
    partial void OnSubjectsChanged(ObservableCollection<SubjectViewModel> value) => OnPropertyChanged(nameof(HasSubjects));

    private static readonly Dictionary<string, string> ViewLabels = new()
    {
        ["Dashboard"]       = "Dashboard",
        ["Learning"]        = "Lernen",
        ["Tests"]           = "Tests",
        ["Analytics"]       = "Analytics",
        ["ContentGenerator"]= "KI-Werkzeuge",
        ["KITools"]         = "KI-Werkzeuge",
        ["Subjects"]        = "Fächer",
        ["Profile"]         = "Profil",
        ["Settings"]        = "Einstellungen",
        ["QA"]              = "Lernen",
    };

    private async Task LoadSubjectsAsync()
    {
        var rows = await _subjectRepo.GetSubjectsAsync();
        Subjects = new ObservableCollection<SubjectViewModel>(rows.Select(s => new SubjectViewModel
        {
            Id            = s.Id,
            Name          = s.Name,
            Icon          = s.Icon,
            Color         = s.Color,
            Progress      = s.Progress,
            QuestionCount = s.QuestionCount,
            SuccessRate   = s.SuccessRate,
            Difficulty    = s.Difficulty.ToString(),
            QuestionsToday= s.QuestionsToday,
            LastStudied   = string.IsNullOrEmpty(s.LastStudied) ? "Noch nie" : s.LastStudied,
        }));
    }

    [RelayCommand]
    private void NavigateTo(string view)
    {
        CurrentView = view;
        Breadcrumb = ViewLabels.TryGetValue(view, out var label) ? label : view;
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    [RelayCommand]
    private void SelectSubject(SubjectViewModel? subject)
    {
        ActiveSubject = subject;
        CurrentView = "Learning";
        Breadcrumb = subject?.Name ?? "Lernen";
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        IsSyncing = true;
        SyncStatus = "Synchronisiert...";
        await LoadSubjectsAsync();
        IsSyncing = false;
        SyncStatus = "Synchronisiert";
    }
}

public partial class SubjectViewModel : ObservableObject
{
    public Guid Id { get; set; } = Guid.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _icon = "∫";
    [ObservableProperty] private string _color = "#5B8CFF";
    [ObservableProperty] private double _progress = 0;
    [ObservableProperty] private string _lastStudied = string.Empty;
    [ObservableProperty] private int _questionCount = 0;
    [ObservableProperty] private double _successRate = 0;
    [ObservableProperty] private string _difficulty = "Mittel";
    [ObservableProperty] private int _questionsToday = 0;

    public string SuccessRatePercent => $"{SuccessRate * 100:F0}%";
    public string ProgressPercent => $"{Progress * 100:F0}%";
    public bool HasQuestionsToday => QuestionsToday > 0;
}
