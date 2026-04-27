using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly SubjectRepository _subjectRepo;
    private readonly UserProgressRepository _progress;
    private readonly AchievementRepository _achievements;
    private readonly MindForgeDbContext _db;

    public DashboardViewModel(
        SubjectRepository subjectRepo,
        UserProgressRepository progress,
        AchievementRepository achievements,
        MindForgeDbContext db)
    {
        _subjectRepo = subjectRepo;
        _progress = progress;
        _achievements = achievements;
        _db = db;

        _level  = UserSession.Level;
        _streak = UserSession.CurrentStreak;
        _bestStreak = UserSession.LongestStreak;
        _currentXP  = UserSession.TotalXP;
        _xpToNextLevel = (int)Math.Pow(Math.Max(1, UserSession.Level), 2) * 50;

        _ = LoadAsync();
    }

    [ObservableProperty] private int _correctToday = 0;
    [ObservableProperty] private int _totalToday = 0;
    [ObservableProperty] private int _timeLearnedMinutes = 0;
    [ObservableProperty] private int _streak;
    [ObservableProperty] private int _bestStreak;
    [ObservableProperty] private int _hoursUntilStreakEnd = 24;
    [ObservableProperty] private bool _streakInDanger = false;

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

    [ObservableProperty] private List<AchievementBadge> _recentAchievements = new();
    [ObservableProperty] private List<SubjectViewModel>  _subjects           = new();
    [ObservableProperty] private List<ActivityItem>      _recentActivity     = new();
    [ObservableProperty] private List<ReviewItem>        _upcomingReviews    = new();

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
    partial void OnUpcomingReviewsChanged(List<ReviewItem> value)
    {
        OnPropertyChanged(nameof(HasUpcomingReviews));
        OnPropertyChanged(nameof(TotalReviews));
    }
    partial void OnQuestionTextChanged(string value) => OnPropertyChanged(nameof(HasQuestion));
    partial void OnTotalTodayChanged(int value)
    {
        OnPropertyChanged(nameof(StatsSubline));
        OnPropertyChanged(nameof(TodayPercent));
        OnPropertyChanged(nameof(TodayPercentText));
    }
    partial void OnCorrectTodayChanged(int value)
    {
        OnPropertyChanged(nameof(StatsSubline));
        OnPropertyChanged(nameof(TodayPercent));
        OnPropertyChanged(nameof(TodayPercentText));
    }
    partial void OnTimeLearnedMinutesChanged(int value) => OnPropertyChanged(nameof(TimeLearnedText));
    partial void OnCurrentXPChanged(int value) => OnPropertyChanged(nameof(XPProgressText));

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

    private async Task LoadAsync()
    {
        // Today's progress
        var global = await _progress.GetGlobalProgressAsync();
        CorrectToday = global.CorrectToday;
        TotalToday   = global.TotalToday;
        Streak       = global.CurrentStreak;
        BestStreak   = global.BestStreak;
        StreakInDanger = global.LastStreakDate.Date < DateTime.UtcNow.Date.AddDays(-1);
        HoursUntilStreakEnd = StreakInDanger
            ? 0
            : Math.Max(0, 24 - DateTime.Now.Hour);

        // XP gained today: count today's correct answers × correct-answer XP
        var todayStart = DateTime.UtcNow.Date;
        var todayCorrect = await _db.Answers.CountAsync(a => a.Timestamp >= todayStart && a.IsCorrect);
        XpGainedToday = todayCorrect * Constants.XP.CorrectAnswer;
        TimeLearnedMinutes = await _db.Answers
            .Where(a => a.Timestamp >= todayStart)
            .SumAsync(a => (int?)a.TimeSpentSeconds) / 60 ?? 0;

        // Subjects
        var subjects = (await _subjectRepo.GetSubjectsAsync()).ToList();
        Subjects = subjects.Select(s => new SubjectViewModel
        {
            Id = s.Id,
            Name = s.Name,
            Icon = s.Icon,
            Color = s.Color,
            Progress = s.Progress,
            QuestionCount = s.QuestionCount,
            SuccessRate = s.SuccessRate,
            Difficulty = s.Difficulty.ToString(),
            QuestionsToday = s.QuestionsToday,
            LastStudied = string.IsNullOrEmpty(s.LastStudied) ? "Noch nie" : s.LastStudied,
        }).ToList();

        // Achievements (4 most-recent unlocks)
        var unlocked = (await _achievements.GetAchievementsAsync())
            .Where(a => a.IsUnlocked)
            .OrderByDescending(a => a.UnlockedAt ?? DateTime.MinValue)
            .Take(4)
            .Select(a => new AchievementBadge
            {
                Name = a.Name, Icon = a.Icon, Rarity = a.Rarity.ToString(),
                Description = a.Description, IsUnlocked = true,
            })
            .ToList();
        RecentAchievements = unlocked;

        // Recent activity: last 6 answers
        var recent = await _db.Answers
            .Include(a => a.Question).ThenInclude(q => q!.Subject)
            .OrderByDescending(a => a.Timestamp)
            .Take(6)
            .ToListAsync();
        RecentActivity = recent.Select(a => new ActivityItem
        {
            Subject = a.Question?.Subject?.Name ?? "—",
            Action = a.IsCorrect ? "Richtig beantwortet" : "Falsch beantwortet",
            Time = FormatRelativeTime(a.Timestamp),
            Type = a.IsCorrect ? "correct" : "wrong",
        }).ToList();

        // Upcoming reviews — group SpacedRepetitionItems by day for next 7 days
        var until = DateTime.UtcNow.Date.AddDays(7);
        var reviews = await _db.SpacedRepetitionItems
            .Where(s => s.NextReviewDate <= until)
            .GroupBy(s => s.NextReviewDate.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();
        UpcomingReviews = reviews.Select(r => new ReviewItem
        {
            Day = FormatRelativeDay(r.Date),
            Count = r.Count,
            Urgent = r.Date <= DateTime.UtcNow.Date,
        }).ToList();
    }

    private static string FormatRelativeTime(DateTime t)
    {
        var diff = DateTime.UtcNow - t;
        if (diff.TotalMinutes < 1) return "gerade eben";
        if (diff.TotalHours < 1)  return $"vor {(int)diff.TotalMinutes} Min";
        if (diff.TotalDays < 1)   return $"vor {(int)diff.TotalHours} Std";
        return $"vor {(int)diff.TotalDays} Tagen";
    }

    private static string FormatRelativeDay(DateTime d)
    {
        var today = DateTime.UtcNow.Date;
        if (d == today) return "Heute";
        if (d == today.AddDays(1)) return "Morgen";
        return d.ToString("ddd dd.MM", new System.Globalization.CultureInfo("de-DE"));
    }

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
