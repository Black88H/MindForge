using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    public DashboardViewModel()
    {
        // Load real values from session (set during login)
        _level  = UserSession.Level;
        _streak = UserSession.CurrentStreak;
        _bestStreak = UserSession.LongestStreak;
        _currentXP  = UserSession.TotalXP;
        // XP to next level: simple formula — 1000 * level
        _xpToNextLevel = Math.Max(1000, UserSession.Level * 1000);
    }

    // Stats (real data — zero for new users)
    [ObservableProperty] private int _correctToday = 0;
    [ObservableProperty] private int _totalToday = 0;
    [ObservableProperty] private int _timeLearnedMinutes = 0;
    [ObservableProperty] private int _streak;
    [ObservableProperty] private int _bestStreak;
    [ObservableProperty] private int _hoursUntilStreakEnd = 24;
    [ObservableProperty] private bool _streakInDanger = false;

    // XP (from session)
    [ObservableProperty] private int _currentXP;
    [ObservableProperty] private int _xpToNextLevel;
    [ObservableProperty] private int _level;
    [ObservableProperty] private int _xpGainedToday = 0;

    // Today's question — empty by default
    [ObservableProperty] private string _currentSubjectTag = string.Empty;
    [ObservableProperty] private string _currentQuestionTag = string.Empty;
    [ObservableProperty] private string _currentDifficultyTag = string.Empty;
    [ObservableProperty] private string _questionNumber = string.Empty;
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private string[] _options = [];
    [ObservableProperty] private int _selectedOptionIndex = -1;
    [ObservableProperty] private bool _isAnswered = false;
    [ObservableProperty] private bool _showExplanation = false;

    // Empty lists for new users
    [ObservableProperty] private List<AchievementBadge> _recentAchievements = new();
    [ObservableProperty] private List<SubjectViewModel>  _subjects           = new();
    [ObservableProperty] private List<ActivityItem>      _recentActivity     = new();
    [ObservableProperty] private List<ReviewItem>        _upcomingReviews    = new();

    // Empty-state visibility
    public bool HasSubjects       => Subjects.Count > 0;
    public bool HasActivity       => RecentActivity.Count > 0;
    public bool HasAchievements   => RecentAchievements.Count > 0;
    public bool HasUpcomingReviews=> UpcomingReviews.Count > 0;
    public bool HasQuestion       => !string.IsNullOrEmpty(QuestionText);

    partial void OnSubjectsChanged(List<SubjectViewModel> value)
    {
        OnPropertyChanged(nameof(HasSubjects));
        OnPropertyChanged(nameof(StatsSubline));
    }
    partial void OnRecentActivityChanged(List<ActivityItem> value)  => OnPropertyChanged(nameof(HasActivity));
    partial void OnRecentAchievementsChanged(List<AchievementBadge> value) => OnPropertyChanged(nameof(HasAchievements));
    partial void OnUpcomingReviewsChanged(List<ReviewItem> value)   => OnPropertyChanged(nameof(HasUpcomingReviews));
    partial void OnQuestionTextChanged(string value)                => OnPropertyChanged(nameof(HasQuestion));

    // Computed
    public string WelcomeText => $"Willkommen zurück, {UserSession.Username}! 👋";
    public string DateLine    => DateTime.Now.ToString("dddd, d. MMMM", new System.Globalization.CultureInfo("de-DE"));
    public string StatsSubline
        => TotalToday > 0
            ? $"Du hast heute {CorrectToday}/{TotalToday} richtig beantwortet · {Streak} Tage Serie"
            : "Noch nichts gelernt heute – fang jetzt an! 🚀";
    public double TodayPercent    => TotalToday > 0 ? (double)CorrectToday / TotalToday : 0;
    public string TodayPercentText=> $"{TodayPercent * 100:F0}%";
    public string TimeLearnedText => TimeLearnedMinutes > 0
        ? $"{TimeLearnedMinutes / 60}h {TimeLearnedMinutes % 60}min"
        : "0 min";
    public double XPProgress      => XpToNextLevel > 0
        ? Math.Clamp((double)CurrentXP / XpToNextLevel, 0.0, 1.0)
        : 0;
    public string XPProgressText  => $"Level {Level}  ·  {CurrentXP}/{XpToNextLevel} XP  ·  Level {Level + 1}";
    public int TotalReviews       => UpcomingReviews.Sum(r => r.Count);

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
    public string Description { get; set; } = string.Empty;
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
