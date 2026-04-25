using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;

namespace MindForge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private string _currentView = "Dashboard";
    [ObservableProperty] private bool _isSidebarCollapsed = false;
    [ObservableProperty] private string _userName = "Jonas";
    [ObservableProperty] private int _level = 12;
    [ObservableProperty] private int _currentXP = 2180;
    [ObservableProperty] private int _xpToNextLevel = 2500;
    [ObservableProperty] private int _streak = 12;
    [ObservableProperty] private string _syncStatus = "Synchronisiert";
    [ObservableProperty] private bool _isSyncing = false;
    [ObservableProperty] private string _breadcrumb = "Dashboard";

    [ObservableProperty]
    private List<SubjectViewModel> _subjects = new()
    {
        new() { Name="Analysis II",     Icon="∫",    Color="#5B8CFF", Progress=0.68, LastStudied="vor 12 Min", QuestionCount=342, SuccessRate=0.81, Difficulty="Mittel", QuestionsToday=18 },
        new() { Name="Quantenmechanik", Icon="ψ",    Color="#BD93F9", Progress=0.42, LastStudied="Gestern",    QuestionCount=187, SuccessRate=0.73, Difficulty="Schwer", QuestionsToday=0  },
        new() { Name="English C1",      Icon="En",   Color="#3fcf8e", Progress=0.91, LastStudied="vor 2 Std",  QuestionCount=512, SuccessRate=0.94, Difficulty="Leicht", QuestionsToday=12 },
        new() { Name="Organ. Chemie",   Icon="⌬",    Color="#ffb547", Progress=0.34, LastStudied="vor 3 Tagen",QuestionCount=124, SuccessRate=0.67, Difficulty="Schwer", QuestionsToday=0  },
        new() { Name="Algorithmen",     Icon="{ }",  Color="#ff6b9d", Progress=0.57, LastStudied="vor 25 Min", QuestionCount=268, SuccessRate=0.79, Difficulty="Mittel", QuestionsToday=17 },
        new() { Name="Genetik",         Icon="◐",    Color="#5eead4", Progress=0.22, LastStudied="vor 5 Tagen",QuestionCount=88,  SuccessRate=0.71, Difficulty="Mittel", QuestionsToday=0  },
    };

    [ObservableProperty] private SubjectViewModel? _activeSubject;

    public double XPProgress => XpToNextLevel > 0 ? (double)CurrentXP / XpToNextLevel : 0;
    public string XPProgressText => $"{CurrentXP} / {XpToNextLevel} XP";

    private static readonly Dictionary<string, string> ViewLabels = new()
    {
        ["Dashboard"]       = "Dashboard",
        ["Learning"]        = "Lernen",
        ["Tests"]           = "Tests",
        ["Analytics"]       = "Analytics",
        ["ContentGenerator"]= "KI-Werkzeuge",
        ["Subjects"]        = "Fächer",
        ["Profile"]         = "Profil",
        ["Settings"]        = "Einstellungen",
        ["QA"]              = "Lernen",
    };

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
        await Task.Delay(1500);
        IsSyncing = false;
        SyncStatus = "Synchronisiert";
    }
}

public partial class SubjectViewModel : ObservableObject
{
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
