using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Services;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class AnalyticsViewModel : ObservableObject
{
    private readonly AnalyticsRepository _analytics;
    private readonly UserProgressRepository _progress;
    private readonly AchievementRepository _achievementsRepo;

    public AnalyticsViewModel(
        AnalyticsRepository analytics,
        UserProgressRepository progress,
        AchievementRepository achievements)
    {
        _analytics = analytics;
        _progress = progress;
        _achievementsRepo = achievements;
        _ = LoadAsync();
    }

    [ObservableProperty] private string _activeTab = "XP";
    public List<string> Tabs { get; } = ["XP", "Streaks", "Fächer", "Zeit", "Achievements"];

    // Summary
    [ObservableProperty] private int _totalXP = UserSession.TotalXP;
    [ObservableProperty] private int _level = UserSession.Level;
    [ObservableProperty] private int _currentStreak = UserSession.CurrentStreak;
    [ObservableProperty] private double _overallSuccess = 0;
    [ObservableProperty] private int _achievementsUnlocked = 0;
    [ObservableProperty] private int _achievementsTotal = 0;

    public string OverallSuccessText => $"{OverallSuccess * 100:F0}%";
    public string AchievementText => $"{AchievementsUnlocked}/{AchievementsTotal}";

    partial void OnOverallSuccessChanged(double value) => OnPropertyChanged(nameof(OverallSuccessText));
    partial void OnAchievementsUnlockedChanged(int value) => OnPropertyChanged(nameof(AchievementText));
    partial void OnAchievementsTotalChanged(int value) => OnPropertyChanged(nameof(AchievementText));

    // Time-series
    [ObservableProperty] private ObservableCollection<ChartPoint> _xpHistory = new();
    [ObservableProperty] private ObservableCollection<ChartPoint> _streakHistory = new();
    [ObservableProperty] private ObservableCollection<SubjectStatItem> _subjectStats = new();
    [ObservableProperty] private ObservableCollection<TimeItem> _timeTracking = new();
    [ObservableProperty] private ObservableCollection<AchievementBadge> _achievements = new();
    [ObservableProperty] private ObservableCollection<string> _recommendations = new();

    public bool HasXpHistory   => XpHistory.Count > 0;
    public bool HasStreakHistory => StreakHistory.Count > 0;
    public bool HasSubjectStats => SubjectStats.Count > 0;
    public bool HasTimeTracking => TimeTracking.Count > 0;
    public bool HasAchievements => Achievements.Count > 0;

    partial void OnXpHistoryChanged(ObservableCollection<ChartPoint> value) => OnPropertyChanged(nameof(HasXpHistory));
    partial void OnStreakHistoryChanged(ObservableCollection<ChartPoint> value) => OnPropertyChanged(nameof(HasStreakHistory));
    partial void OnSubjectStatsChanged(ObservableCollection<SubjectStatItem> value) => OnPropertyChanged(nameof(HasSubjectStats));
    partial void OnTimeTrackingChanged(ObservableCollection<TimeItem> value)
    {
        OnPropertyChanged(nameof(HasTimeTracking));
        OnPropertyChanged(nameof(TotalHours));
        OnPropertyChanged(nameof(TotalHoursText));
    }
    partial void OnAchievementsChanged(ObservableCollection<AchievementBadge> value) => OnPropertyChanged(nameof(HasAchievements));

    public double TotalHours => TimeTracking.Sum(t => t.Hours);
    public string TotalHoursText => $"{TotalHours:F1} Std. gesamt";

    public int LongestStreak => UserSession.LongestStreak;
    public int TotalActiveDays { get; private set; }
    public int TotalMinutesThisWeek { get; private set; }
    public int AvgMinutesPerDay { get; private set; }
    public string BestDay { get; private set; } = "—";
    public ObservableCollection<TimeItem> TimeData => TimeTracking;
    public int UnlockedCount => AchievementsUnlocked;

    private async Task LoadAsync()
    {
        OverallSuccess = await _analytics.GetOverallSuccessRateAsync();
        var allAchievements = (await _achievementsRepo.GetAchievementsAsync()).ToList();
        AchievementsTotal    = allAchievements.Count;
        AchievementsUnlocked = allAchievements.Count(a => a.IsUnlocked);

        Achievements = new ObservableCollection<AchievementBadge>(allAchievements.Select(a => new AchievementBadge
        {
            Name = a.Name,
            Icon = a.Icon,
            Rarity = a.Rarity.ToString(),
            Description = a.Description,
            IsUnlocked = a.IsUnlocked,
        }));

        // 30-day XP history
        var xp = (await _analytics.GetXPHistoryAsync(30)).ToList();
        var xpDict = xp.ToDictionary(x => x.Date.Date, x => x.XP);
        var maxXp = xpDict.Values.DefaultIfEmpty(0).Max();
        var maxXpScale = Math.Max(50, maxXp);
        var xpPoints = new List<ChartPoint>();
        for (int i = 29; i >= 0; i--)
        {
            var d = DateTime.UtcNow.Date.AddDays(-i);
            xpPoints.Add(new ChartPoint
            {
                Label = d.ToString("dd.MM"),
                Value = xpDict.TryGetValue(d, out var v) ? v : 0,
                MaxValue = maxXpScale,
            });
        }
        XpHistory = new ObservableCollection<ChartPoint>(xpPoints);

        // 14-day streak history (running streak per day, derived from daily activity)
        var activity = (await _analytics.GetDailyActivityAsync(14)).ToList();
        var actDict = activity.ToDictionary(a => a.Date.Date, a => a.Questions);
        var streakPoints = new List<ChartPoint>();
        int running = 0;
        for (int i = 13; i >= 0; i--)
        {
            var d = DateTime.UtcNow.Date.AddDays(-i);
            running = actDict.GetValueOrDefault(d, 0) > 0 ? running + 1 : 0;
            streakPoints.Add(new ChartPoint { Label = d.ToString("dd.MM"), Value = running, MaxValue = 14 });
        }
        StreakHistory = new ObservableCollection<ChartPoint>(streakPoints);

        // Subject stats (ranked)
        var stats = (await _analytics.GetSubjectStatsAsync()).ToList();
        SubjectStats = new ObservableCollection<SubjectStatItem>(stats.Select((tuple, idx) => new SubjectStatItem
        {
            Rank = $"#{idx + 1}",
            Icon = tuple.Subject.Icon,
            Name = tuple.Subject.Name,
            SuccessRate = tuple.SuccessRate,
            Questions = tuple.Subject.QuestionCount,
            Level = Math.Max(1, (int)Math.Round(tuple.Subject.Progress * 10)),
        }));

        // Time tracking (per subject)
        var times = (await _analytics.GetTimePerSubjectAsync()).ToList();
        var palette = new[] { "#5B8CFF", "#3fcf8e", "#ff6b9d", "#BD93F9", "#ffb547", "#5eead4", "#FF6B35", "#4CAF50" };
        TimeTracking = new ObservableCollection<TimeItem>(times.Select((t, i) => new TimeItem
        {
            SubjectName = t.Subject,
            Hours = t.Minutes / 60.0,
            Color = palette[i % palette.Length],
        }));

        // Time-tab summary
        var weekActivity = activity.Where(a => a.Date >= DateTime.UtcNow.Date.AddDays(-7)).ToList();
        TotalMinutesThisWeek = weekActivity.Sum(a => a.Questions); // 1 question ≈ 1 minute approximation
        AvgMinutesPerDay     = weekActivity.Count > 0 ? TotalMinutesThisWeek / 7 : 0;
        BestDay              = weekActivity.OrderByDescending(a => a.Questions)
                                           .Select(a => a.Date.ToString("ddd",
                                               new System.Globalization.CultureInfo("de-DE")))
                                           .FirstOrDefault() ?? "—";
        TotalActiveDays      = activity.Count(a => a.Questions > 0);

        OnPropertyChanged(nameof(LongestStreak));
        OnPropertyChanged(nameof(TotalActiveDays));
        OnPropertyChanged(nameof(TotalMinutesThisWeek));
        OnPropertyChanged(nameof(AvgMinutesPerDay));
        OnPropertyChanged(nameof(BestDay));
        OnPropertyChanged(nameof(UnlockedCount));

        // Recommendations from weak areas
        var weak = await _analytics.GetWeaknessAreasAsync();
        var recs = new List<string>();
        foreach (var (subject, count) in weak.OrderByDescending(kv => kv.Value).Take(3))
            recs.Add($"{subject}: {count} schwache Fragen — wiederholen lohnt sich.");
        if (UserSession.CurrentStreak == 0)
            recs.Add("Dein Streak ist gerissen — eine kurze Session heute startet ihn neu.");
        if (recs.Count == 0)
            recs.Add("Keine Auffälligkeiten — weiter so!");
        Recommendations = new ObservableCollection<string>(recs);
    }

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private async Task RefreshChartsAsync() => await LoadAsync();
}

public class ChartPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public double MaxValue { get; set; } = 500;
    public double NormalizedHeight => Math.Clamp(Value / MaxValue, 0, 1) * 120;
    public bool IsActive => Value > 0;
}

public class SubjectStatItem
{
    public string Rank { get; set; } = string.Empty;
    public string Icon { get; set; } = "∫";
    public string Name { get; set; } = string.Empty;
    public double SuccessRate { get; set; }
    public string SuccessText => $"{SuccessRate * 100:F0}%";
    public string SuccessColor => SuccessRate >= 0.80 ? "#3FCF8E" : SuccessRate >= 0.65 ? "#FFB547" : "#FF6B6B";
    public int Questions { get; set; }
    public int Level { get; set; }
}

public class TimeItem
{
    public string SubjectName { get; set; } = string.Empty;
    public double Hours { get; set; }
    public string Color { get; set; } = "#5B8CFF";
    public string HoursText => $"{Hours:F1}h";
    public string Label => SubjectName.Length > 9 ? SubjectName[..9] + "…" : SubjectName;
    public string ValueText => $"{Hours:F1}h";
    public double NormalizedHeight => Math.Clamp(Hours / 25.0, 0, 1) * 120;
}
