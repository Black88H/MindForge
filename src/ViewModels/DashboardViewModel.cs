using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MindForge.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    // Stats
    [ObservableProperty] private int _correctToday = 47;
    [ObservableProperty] private int _totalToday = 60;
    [ObservableProperty] private int _timeLearnedMinutes = 135;
    [ObservableProperty] private int _streak = 12;
    [ObservableProperty] private int _bestStreak = 18;
    [ObservableProperty] private int _hoursUntilStreakEnd = 3;
    [ObservableProperty] private bool _streakInDanger = true;

    // XP
    [ObservableProperty] private int _currentXP = 2180;
    [ObservableProperty] private int _xpToNextLevel = 2500;
    [ObservableProperty] private int _level = 12;
    [ObservableProperty] private int _xpGainedToday = 468;

    // Today's question
    [ObservableProperty] private string _currentSubjectTag = "Analysis II";
    [ObservableProperty] private string _currentQuestionTag = "Reihen";
    [ObservableProperty] private string _currentDifficultyTag = "Mittel";
    [ObservableProperty] private string _questionNumber = "#047 · 13 fällig";
    [ObservableProperty] private string _questionText = "Welche Reihe konvergiert nach dem Leibniz-Kriterium, aber nicht absolut?";
    [ObservableProperty] private string[] _options = ["∑ 1/n²", "∑ (-1)ⁿ / n", "∑ (-1)ⁿ / n²", "∑ 1/√n"];
    [ObservableProperty] private int _selectedOptionIndex = -1;
    [ObservableProperty] private bool _isAnswered = false;
    [ObservableProperty] private bool _showExplanation = false;

    // Recent achievements
    [ObservableProperty] private List<AchievementBadge> _recentAchievements = new()
    {
        new() { Name="Erster Schritt", Icon="🥾", Rarity="Häufig",  IsUnlocked=true  },
        new() { Name="Wochenkrieger", Icon="⚔️",  Rarity="Häufig",  IsUnlocked=true  },
        new() { Name="Perfekte Zehn", Icon="💜",  Rarity="Selten",  IsUnlocked=true  },
        new() { Name="Nachteule",     Icon="🦉",  Rarity="Häufig",  IsUnlocked=true  },
    };

    // Subjects (für die Dashboard-Karten)
    [ObservableProperty] private List<SubjectViewModel> _subjects = new()
    {
        new() { Name="Analysis II",     Icon="∫",   Color="#5B8CFF", Progress=0.68, LastStudied="vor 12 Min", QuestionCount=342, SuccessRate=0.81, Difficulty="Mittel", QuestionsToday=18 },
        new() { Name="Quantenmechanik", Icon="ψ",   Color="#BD93F9", Progress=0.42, LastStudied="Gestern",    QuestionCount=187, SuccessRate=0.73, Difficulty="Schwer", QuestionsToday=0  },
        new() { Name="English C1",      Icon="En",  Color="#3fcf8e", Progress=0.91, LastStudied="vor 2 Std",  QuestionCount=512, SuccessRate=0.94, Difficulty="Leicht", QuestionsToday=12 },
        new() { Name="Organ. Chemie",   Icon="⌬",   Color="#ffb547", Progress=0.34, LastStudied="vor 3 Tagen",QuestionCount=124, SuccessRate=0.67, Difficulty="Schwer", QuestionsToday=0  },
        new() { Name="Algorithmen",     Icon="{ }", Color="#ff6b9d", Progress=0.57, LastStudied="vor 25 Min", QuestionCount=268, SuccessRate=0.79, Difficulty="Mittel", QuestionsToday=17 },
        new() { Name="Genetik",         Icon="◐",   Color="#5eead4", Progress=0.22, LastStudied="vor 5 Tagen",QuestionCount=88,  SuccessRate=0.71, Difficulty="Mittel", QuestionsToday=0  },
    };

    // Recent activity
    [ObservableProperty] private List<ActivityItem> _recentActivity = new()
    {
        new() { Subject="Analysis II",  Action="Cauchy-Kriterium · 8 Fragen · 100%",     Time="vor 12 Min", Type="correct" },
        new() { Subject="Algorithmen",  Action="KI generierte 24 Karten aus Dijkstra.pdf",Time="vor 28 Min", Type="generate" },
        new() { Subject="Analysis II",  Action="Taylor-Reihen · 12 Fragen · 58% Erfolg", Time="vor 1 Std",  Type="partial" },
        new() { Subject="English C1",   Action="Quick Check abgeschlossen · 92%",         Time="vor 2 Std",  Type="correct" },
    };

    // Upcoming reviews
    [ObservableProperty] private List<ReviewItem> _upcomingReviews = new()
    {
        new() { Day="Heute",     Count=13, Urgent=true },
        new() { Day="Morgen",    Count=28, Urgent=false },
        new() { Day="Übermorgen",Count=41, Urgent=false },
        new() { Day="Fr, 26.04.",Count=19, Urgent=false },
        new() { Day="Sa, 27.04.",Count=7,  Urgent=false },
        new() { Day="So, 28.04.",Count=12, Urgent=false },
    };

    // Computed
    public string DateLine => DateTime.Now.ToString("dddd, d. MMMM", new System.Globalization.CultureInfo("de-DE"));
    public string StatsSubline => $"Du hast heute {CorrectToday}/{TotalToday} richtig beantwortet · {Streak} Tage Serie";
    public double TodayPercent => TotalToday > 0 ? (double)CorrectToday / TotalToday : 0;
    public string TodayPercentText => $"{TodayPercent * 100:F0}%";
    public string TimeLearnedText => $"{TimeLearnedMinutes / 60}h {TimeLearnedMinutes % 60}min";
    public double XPProgress => XpToNextLevel > 0
        ? Math.Clamp((double)CurrentXP / XpToNextLevel, 0.0, 1.0)
        : 0;
    public string XPProgressText => $"Level {Level}  ·  {CurrentXP}/{XpToNextLevel} XP  ·  Level {Level + 1}";
    public int TotalReviews => UpcomingReviews.Sum(r => r.Count);

    [RelayCommand]
    private void SelectOption(int index)
    {
        if (IsAnswered) return;
        SelectedOptionIndex = index;
    }

    [RelayCommand]
    private void SubmitAnswer()
    {
        if (SelectedOptionIndex < 0) return;
        IsAnswered = true;
    }

    [RelayCommand]
    private void ShowAIExplanation() => ShowExplanation = true;

    [RelayCommand]
    private void NextQuestion()
    {
        IsAnswered = false;
        SelectedOptionIndex = -1;
        ShowExplanation = false;
    }
}

public class AchievementBadge
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "🏆";
    public string Rarity { get; set; } = "Häufig";
    public bool IsUnlocked { get; set; } = true;
}

public class ActivityItem
{
    public string Subject { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Type { get; set; } = "correct";
}

public class ReviewItem
{
    public string Day { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool Urgent { get; set; }
    public double MaxPercent => 1.0;
}
