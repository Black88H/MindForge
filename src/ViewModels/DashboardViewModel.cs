using System.Collections.ObjectModel;
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
    private readonly UserProgressRepository _progressRepo;
    private readonly AchievementRepository _achievementRepo;
    private readonly MindForgeDbContext _db;
    private readonly ILearningPlanService _planService;
    private readonly ISpacedRepetitionService _srService;
    private readonly IGamificationService _gamification;
    private readonly IAPIKeyService _apiKeys;

    public DashboardViewModel(
        SubjectRepository subjectRepo,
        UserProgressRepository progress,
        AchievementRepository achievements,
        MindForgeDbContext db,
        ILearningPlanService planService,
        ISpacedRepetitionService srService,
        IGamificationService gamification,
        IAPIKeyService apiKeys)
    {
        _subjectRepo = subjectRepo;
        _progressRepo = progress;
        _achievementRepo = achievements;
        _db = db;
        _planService = planService;
        _srService = srService;
        _gamification = gamification;
        _apiKeys = apiKeys;

        _userName = UserSession.Username;
        _level = UserSession.Level;
        _streak = UserSession.CurrentStreak;
        _bestStreak = UserSession.LongestStreak;
        _currentXP = UserSession.TotalXP;
        _xpToNextLevel = _gamification.GetXPForLevel(UserSession.Level + 1);
        _levelStartXP = _gamification.GetXPForLevel(UserSession.Level);

        _ = LoadAllAsync();
    }

    // ── MODULE 1: GAMIFICATION ──────────────────────────────────────────────
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private int _level;
    [ObservableProperty] private int _currentXP;
    [ObservableProperty] private int _xpToNextLevel;
    [ObservableProperty] private int _levelStartXP;
    [ObservableProperty] private int _xpGainedToday;
    [ObservableProperty] private int _streak;
    [ObservableProperty] private int _bestStreak;
    [ObservableProperty] private int _hoursUntilStreakEnd = 24;
    [ObservableProperty] private bool _streakInDanger;
    [ObservableProperty] private ObservableCollection<StreakDay> _streakDays = new();
    [ObservableProperty] private ObservableCollection<XPLogEntry> _xpLog = new();
    [ObservableProperty] private List<AchievementBadge> _recentAchievements = new();

    public double LevelProgress => (XpToNextLevel - LevelStartXP) > 0
        ? Math.Clamp((double)(CurrentXP - LevelStartXP) / (XpToNextLevel - LevelStartXP), 0, 1) : 0;
    public string LevelProgressTooltip => $"Noch {Math.Max(0, XpToNextLevel - CurrentXP)} XP bis Level {Level + 1}";
    public string XPProgressText => $"Level {Level}  ·  {CurrentXP}/{XpToNextLevel} XP";

    partial void OnCurrentXPChanged(int value) { OnPropertyChanged(nameof(LevelProgress)); OnPropertyChanged(nameof(LevelProgressTooltip)); OnPropertyChanged(nameof(XPProgressText)); }
    partial void OnXpToNextLevelChanged(int value) { OnPropertyChanged(nameof(LevelProgress)); OnPropertyChanged(nameof(LevelProgressTooltip)); OnPropertyChanged(nameof(XPProgressText)); }
    partial void OnLevelStartXPChanged(int value) { OnPropertyChanged(nameof(LevelProgress)); OnPropertyChanged(nameof(LevelProgressTooltip)); }
    partial void OnRecentAchievementsChanged(List<AchievementBadge> value) => OnPropertyChanged(nameof(HasAchievements));
    public bool HasAchievements => RecentAchievements.Count > 0;

    // ── MODULE 2: TAGES-LERNPLAN ────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<TaskItemVM> _todayTasks = new();
    [ObservableProperty] private int _totalTaskMinutes;
    [ObservableProperty] private int _completedTaskCount;

    public string TaskSummary => $"Gesamtzeit für heute: {TotalTaskMinutes / 60}h {TotalTaskMinutes % 60}min";
    public bool HasTasks => TodayTasks.Count > 0;
    partial void OnTodayTasksChanged(ObservableCollection<TaskItemVM> value) { OnPropertyChanged(nameof(HasTasks)); OnPropertyChanged(nameof(TaskSummary)); }
    partial void OnTotalTaskMinutesChanged(int value) => OnPropertyChanged(nameof(TaskSummary));

    // ── MODULE 3: SPACED REPETITION ─────────────────────────────────────────
    [ObservableProperty] private int _dueReviewCount;
    [ObservableProperty] private List<ReviewItem> _upcomingReviews = new();
    public bool HasUpcomingReviews => UpcomingReviews.Count > 0;
    public int TotalReviews => UpcomingReviews.Sum(r => r.Count);
    partial void OnUpcomingReviewsChanged(List<ReviewItem> value) { OnPropertyChanged(nameof(HasUpcomingReviews)); OnPropertyChanged(nameof(TotalReviews)); }

    // ── MODULE 4: FÄCHER ────────────────────────────────────────────────────
    [ObservableProperty] private List<SubjectViewModel> _subjects = new();
    public bool HasSubjects => Subjects.Count > 0;
    partial void OnSubjectsChanged(List<SubjectViewModel> value) => OnPropertyChanged(nameof(HasSubjects));

    // ── MODULE 5: HEUTE LERNEN (Hero Card) ──────────────────────────────────
    [ObservableProperty] private string _currentSubjectTag = string.Empty;
    [ObservableProperty] private string _currentQuestionTag = string.Empty;
    [ObservableProperty] private string _currentDifficultyTag = string.Empty;
    [ObservableProperty] private string _questionNumber = string.Empty;
    [ObservableProperty] private string _questionText = string.Empty;
    [ObservableProperty] private int _selectedOptionIndex = -1;
    [ObservableProperty] private bool _isAnswered;
    [ObservableProperty] private bool _showExplanation;
    public bool HasQuestion => !string.IsNullOrEmpty(QuestionText);
    partial void OnQuestionTextChanged(string value) => OnPropertyChanged(nameof(HasQuestion));

    // ── MODULE 6: SYSTEM STATUS ─────────────────────────────────────────────
    [ObservableProperty] private string _openAIStatus = "Prüfe...";
    [ObservableProperty] private string _anthropicStatus = "Prüfe...";
    [ObservableProperty] private string _ollamaStatus = "Prüfe...";
    [ObservableProperty] private string _syncStatusText = "Bereit";

    // ── STATS ───────────────────────────────────────────────────────────────
    [ObservableProperty] private int _correctToday;
    [ObservableProperty] private int _totalToday;
    [ObservableProperty] private int _timeLearnedMinutes;
    [ObservableProperty] private List<ActivityItem> _recentActivity = new();
    public bool HasActivity => RecentActivity.Count > 0;
    partial void OnRecentActivityChanged(List<ActivityItem> value) => OnPropertyChanged(nameof(HasActivity));

    public string DateLine => DateTime.Now.ToString("dddd, d. MMMM", new System.Globalization.CultureInfo("de-DE"));
    public string StatsSubline => TotalToday > 0
        ? $"Du hast heute {CorrectToday}/{TotalToday} richtig beantwortet · {Streak} Tage Serie"
        : "Noch nichts gelernt heute – fang jetzt an! 🚀";
    public double TodayPercent => TotalToday > 0 ? (double)CorrectToday / TotalToday : 0;
    public string TodayPercentText => $"{TodayPercent * 100:F0}%";
    public string TimeLearnedText => TimeLearnedMinutes > 0 ? $"{TimeLearnedMinutes / 60}h {TimeLearnedMinutes % 60}min" : "0 min";

    partial void OnTotalTodayChanged(int value) { OnPropertyChanged(nameof(StatsSubline)); OnPropertyChanged(nameof(TodayPercent)); OnPropertyChanged(nameof(TodayPercentText)); }
    partial void OnCorrectTodayChanged(int value) { OnPropertyChanged(nameof(StatsSubline)); OnPropertyChanged(nameof(TodayPercent)); OnPropertyChanged(nameof(TodayPercentText)); }
    partial void OnTimeLearnedMinutesChanged(int value) => OnPropertyChanged(nameof(TimeLearnedText));

    // ═══════════════════════════════════════════════════════════════════════
    //  PARALLEL LOAD
    // ═══════════════════════════════════════════════════════════════════════
    private async Task LoadAllAsync()
    {
        try
        {
            // All loads run, errors are caught individually
            await Task.WhenAll(
                LoadProgressAsync(),
                LoadTasksAsync(),
                LoadReviewsAsync(),
                LoadSubjectsAsync(),
                LoadXPLogAsync(),
                LoadAchievementsAsync(),
                LoadActivityAsync(),
                LoadStreakDaysAsync(),
                CheckAPIStatusAsync()
            );
        }
        catch (Exception ex)
        {
            Logger.Error("Dashboard LoadAllAsync Fehler", ex);
        }

        // Dummy fallback for dev mode
        PopulateDevDataIfEmpty();
    }

    private async Task LoadProgressAsync()
    {
        try
        {
            var global = await _progressRepo.GetGlobalProgressAsync();
            CorrectToday = global.CorrectToday;
            TotalToday = global.TotalToday;
            Streak = global.CurrentStreak;
            BestStreak = global.BestStreak;
            StreakInDanger = global.LastStreakDate.Date < DateTime.UtcNow.Date.AddDays(-1);
            HoursUntilStreakEnd = StreakInDanger ? 0 : Math.Max(0, 24 - DateTime.Now.Hour);

            var todayStart = DateTime.UtcNow.Date;
            var todayCorrect = await _db.Answers.CountAsync(a => a.Timestamp >= todayStart && a.IsCorrect);
            XpGainedToday = todayCorrect * Constants.XP.CorrectAnswer;
            TimeLearnedMinutes = await _db.Answers
                .Where(a => a.Timestamp >= todayStart)
                .SumAsync(a => (int?)a.TimeSpentSeconds) / 60 ?? 0;
        }
        catch (Exception ex) { Logger.Error("LoadProgress", ex); }
    }

    private async Task LoadTasksAsync()
    {
        try
        {
            var tasks = await _planService.GetTodaysTasksAsync(UserSession.UserId);
            var items = tasks.Select(t => new TaskItemVM
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                DurationMinutes = t.DurationMinutes,
                Priority = t.Priority,
                IsOverdue = t.ScheduledDate.Date < DateTime.UtcNow.Date,
                IsCompleted = t.CompletedAt != null,
                TypeIcon = t.TaskType switch
                {
                    LearningTaskType.Review => "🔄",
                    LearningTaskType.NewContent => "📖",
                    LearningTaskType.Test => "📝",
                    LearningTaskType.FeynmanCheck => "🧪",
                    _ => "📋"
                }
            }).ToList();
            TodayTasks = new ObservableCollection<TaskItemVM>(items);
            TotalTaskMinutes = items.Sum(t => t.DurationMinutes);
            CompletedTaskCount = items.Count(t => t.IsCompleted);
        }
        catch (Exception ex) { Logger.Error("LoadTasks", ex); }
    }

    private async Task LoadReviewsAsync()
    {
        try
        {
            var due = await _srService.GetDueReviewsAsync(UserSession.UserId);
            DueReviewCount = due.Count;

            var until = DateTime.UtcNow.Date.AddDays(7);
            var reviews = await _db.SpacedRepetitionItems
                .Where(s => s.NextReviewDate <= until)
                .GroupBy(s => s.NextReviewDate.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();
            UpcomingReviews = reviews.Select(r => new ReviewItem
            {
                Day = FormatRelativeDay(r.Date), Count = r.Count,
                Urgent = r.Date <= DateTime.UtcNow.Date,
            }).ToList();
        }
        catch (Exception ex) { Logger.Error("LoadReviews", ex); }
    }

    private async Task LoadSubjectsAsync()
    {
        try
        {
            var subjects = (await _subjectRepo.GetSubjectsAsync()).ToList();
            Subjects = subjects.Select(s => new SubjectViewModel
            {
                Id = s.Id, Name = s.Name, Icon = s.Icon, Color = s.Color,
                Progress = s.Progress, QuestionCount = s.QuestionCount,
                SuccessRate = s.SuccessRate, Difficulty = s.Difficulty.ToString(),
                QuestionsToday = s.QuestionsToday,
                LastStudied = string.IsNullOrEmpty(s.LastStudied) ? "Noch nie" : s.LastStudied,
            }).ToList();
        }
        catch (Exception ex) { Logger.Error("LoadSubjects", ex); }
    }

    private async Task LoadXPLogAsync()
    {
        try
        {
            var events = await _db.XPEvents
                .Where(e => e.UserId == UserSession.UserId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(10)
                .ToListAsync();
            XpLog = new ObservableCollection<XPLogEntry>(events.Select(e => new XPLogEntry
            {
                Amount = e.Amount, Description = e.Description,
                Time = FormatRelativeTime(e.CreatedAt),
                Source = e.Source.ToString()
            }));
        }
        catch (Exception ex) { Logger.Error("LoadXPLog", ex); }
    }

    private async Task LoadAchievementsAsync()
    {
        try
        {
            var unlocked = (await _achievementRepo.GetAchievementsAsync())
                .Where(a => a.IsUnlocked).OrderByDescending(a => a.UnlockedAt ?? DateTime.MinValue)
                .Take(4).Select(a => new AchievementBadge
                {
                    Name = a.Name, Icon = a.Icon, Rarity = a.Rarity.ToString(),
                    Description = a.Description, IsUnlocked = true,
                }).ToList();
            RecentAchievements = unlocked;
        }
        catch (Exception ex) { Logger.Error("LoadAchievements", ex); }
    }

    private async Task LoadActivityAsync()
    {
        try
        {
            var recent = await _db.Answers
                .Include(a => a.Question).ThenInclude(q => q!.Subject)
                .OrderByDescending(a => a.Timestamp).Take(6).ToListAsync();
            RecentActivity = recent.Select(a => new ActivityItem
            {
                Subject = a.Question?.Subject?.Name ?? "—",
                Action = a.IsCorrect ? "Richtig beantwortet" : "Falsch beantwortet",
                Time = FormatRelativeTime(a.Timestamp),
                Type = a.IsCorrect ? "correct" : "wrong",
            }).ToList();
        }
        catch (Exception ex) { Logger.Error("LoadActivity", ex); }
    }

    private async Task LoadStreakDaysAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var days = new List<StreakDay>();
            for (int i = 6; i >= 0; i--)
            {
                var date = today.AddDays(-i);
                var hasActivity = await _db.XPEvents.AnyAsync(e => e.UserId == UserSession.UserId && e.CreatedAt.Date == date);
                days.Add(new StreakDay
                {
                    DayLabel = date.ToString("dd", new System.Globalization.CultureInfo("de-DE")),
                    DayName = date.ToString("ddd", new System.Globalization.CultureInfo("de-DE")),
                    IsActive = hasActivity,
                    IsToday = i == 0
                });
            }
            StreakDays = new ObservableCollection<StreakDay>(days);
        }
        catch (Exception ex) { Logger.Error("LoadStreakDays", ex); }
    }

    private async Task CheckAPIStatusAsync()
    {
        try
        {
            var openai = await _apiKeys.GetKeyAsync("openai");
            OpenAIStatus = openai != null ? "✅ Bereit" : "⚠ Key fehlt";
            var anthropic = await _apiKeys.GetKeyAsync("anthropic");
            AnthropicStatus = anthropic != null ? "✅ Bereit" : "⚠ Key fehlt";
            OllamaStatus = "✅ Lokal";
        }
        catch (Exception ex) { Logger.Error("CheckAPI", ex); OllamaStatus = "❌ Fehler"; }
    }

    // ── DEV FALLBACK ────────────────────────────────────────────────────────
    private void PopulateDevDataIfEmpty()
    {
        if (Subjects.Count == 0)
            Subjects = new List<SubjectViewModel>
            {
                new() { Name = "Mathematik", Icon = "∑", Color = "#5B8CFF", Progress = 0.4, QuestionCount = 120, SuccessRate = 0.85, Difficulty = "Mittel", QuestionsToday = 5, LastStudied = "Heute" },
                new() { Name = "Informatik", Icon = "💻", Color = "#10B981", Progress = 0.7, QuestionCount = 340, SuccessRate = 0.92, Difficulty = "Schwer", QuestionsToday = 0, LastStudied = "Gestern" },
                new() { Name = "Physik", Icon = "⚛", Color = "#8B5CF6", Progress = 0.2, QuestionCount = 45, SuccessRate = 0.60, Difficulty = "Leicht", QuestionsToday = 12, LastStudied = "Heute" }
            };

        if (RecentAchievements.Count == 0)
            RecentAchievements = new List<AchievementBadge>
            {
                new() { Name = "Erster Login", Icon = "🚀", Rarity = "Häufig" },
                new() { Name = "10 Fragen", Icon = "🧠", Rarity = "Häufig" },
                new() { Name = "Fehlerfrei", Icon = "⭐", Rarity = "Selten" },
                new() { Name = "7-Tage-Streak", Icon = "🔥", Rarity = "Episch" }
            };

        if (RecentActivity.Count == 0)
            RecentActivity = new List<ActivityItem>
            {
                new() { Subject = "Informatik (Graphen)", Action = "Richtig beantwortet", Time = "vor 10 Min", Type = "correct" },
                new() { Subject = "Physik (Kinematik)", Action = "Falsch beantwortet", Time = "vor 2 Std", Type = "wrong" },
                new() { Subject = "Mathematik (Analysis)", Action = "Richtig beantwortet", Time = "gestern", Type = "correct" }
            };

        if (UpcomingReviews.Count == 0)
            UpcomingReviews = new List<ReviewItem>
            {
                new() { Day = "Heute", Count = 15, Urgent = true },
                new() { Day = "Morgen", Count = 8, Urgent = false },
                new() { Day = "Übermorgen", Count = 12, Urgent = false }
            };

        if (TodayTasks.Count == 0)
            TodayTasks = new ObservableCollection<TaskItemVM>
            {
                new() { Title = "Neuroanatomie: Hirnnerven lernen", Description = "Kapitel 3.2 durcharbeiten", DurationMinutes = 25, Priority = 1, IsOverdue = true, TypeIcon = "📖" },
                new() { Title = "Physik: Kinematik wiederholen", Description = "Spaced Repetition Review", DurationMinutes = 15, Priority = 2, TypeIcon = "🔄" },
                new() { Title = "Mathe: Integration üben", Description = "5 Übungsaufgaben lösen", DurationMinutes = 20, Priority = 3, TypeIcon = "📝" },
                new() { Title = "Feynman-Check: Elektrodynamik", Description = "Erkläre Maxwell-Gleichungen", DurationMinutes = 15, Priority = 4, TypeIcon = "🧪" },
            };
        if (TotalTaskMinutes == 0) TotalTaskMinutes = TodayTasks.Sum(t => t.DurationMinutes);

        if (XpLog.Count == 0)
            XpLog = new ObservableCollection<XPLogEntry>
            {
                new() { Amount = 50, Description = "Kurs 'Neuroanatomie' abgeschlossen", Time = "vor 2 Std", Source = "LessonCompleted" },
                new() { Amount = 15, Description = "3 Wiederholungen korrekt", Time = "vor 4 Std", Source = "TestCompleted" },
                new() { Amount = 10, Description = "Täglicher Login-Bonus", Time = "heute", Source = "StreakBonus" },
            };

        if (StreakDays.Count == 0)
        {
            var today = DateTime.UtcNow.Date;
            StreakDays = new ObservableCollection<StreakDay>();
            for (int i = 6; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                StreakDays.Add(new StreakDay
                {
                    DayLabel = d.ToString("dd"), DayName = d.ToString("ddd", new System.Globalization.CultureInfo("de-DE")),
                    IsActive = i <= 3, IsToday = i == 0
                });
            }
        }

        if (string.IsNullOrEmpty(QuestionText))
        {
            CurrentSubjectTag = "Mathematik";
            CurrentQuestionTag = "Analysis";
            CurrentDifficultyTag = "Mittel";
            QuestionNumber = "#1042";
            QuestionText = "Welche der folgenden Reihen konvergiert absolut?";
        }

        if (DueReviewCount == 0) DueReviewCount = 15;
        if (OpenAIStatus == "Prüfe...") OpenAIStatus = "⚠ Key fehlt";
        if (AnthropicStatus == "Prüfe...") AnthropicStatus = "⚠ Key fehlt";
        if (OllamaStatus == "Prüfe...") OllamaStatus = "✅ Lokal";
    }

    // ── COMMANDS ────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task CompleteTaskAsync(TaskItemVM? task)
    {
        if (task == null || task.IsCompleted) return;
        try
        {
            await _planService.CompleteTaskAsync(task.Id, UserSession.UserId);
            task.IsCompleted = true;
            CompletedTaskCount++;
            await LoadProgressAsync(); // refresh XP
        }
        catch (Exception ex) { Logger.Error("CompleteTask", ex); }
    }

    [RelayCommand] private void SelectOption(int index) { if (!IsAnswered) SelectedOptionIndex = index; }
    [RelayCommand] private void SubmitAnswer() { if (SelectedOptionIndex >= 0) IsAnswered = true; }
    [RelayCommand] private void ShowAIExplanation() => ShowExplanation = true;
    [RelayCommand] private void NextQuestion() { IsAnswered = false; SelectedOptionIndex = -1; ShowExplanation = false; }

    // ── HELPERS ─────────────────────────────────────────────────────────────
    private static string FormatRelativeTime(DateTime t)
    {
        var diff = DateTime.UtcNow - t;
        if (diff.TotalMinutes < 1) return "gerade eben";
        if (diff.TotalHours < 1) return $"vor {(int)diff.TotalMinutes} Min";
        if (diff.TotalDays < 1) return $"vor {(int)diff.TotalHours} Std";
        return $"vor {(int)diff.TotalDays} Tagen";
    }

    private static string FormatRelativeDay(DateTime d)
    {
        var today = DateTime.UtcNow.Date;
        if (d == today) return "Heute";
        if (d == today.AddDays(1)) return "Morgen";
        return d.ToString("ddd dd.MM", new System.Globalization.CultureInfo("de-DE"));
    }
}

// ── HELPER MODELS ───────────────────────────────────────────────────────────

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
}

public partial class TaskItemVM : ObservableObject
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public int Priority { get; set; }
    public bool IsOverdue { get; set; }
    [ObservableProperty] private bool _isCompleted;
    public string TypeIcon { get; set; } = "📋";
    public string DurationText => $"{DurationMinutes} Min";
    public string PriorityTag => Priority switch { 1 => "Überfällig", 2 => "Heute", 3 => "Geplant", _ => "Optional" };
}

public class StreakDay
{
    public string DayLabel { get; set; } = string.Empty;
    public string DayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsToday { get; set; }
}

public class XPLogEntry
{
    public int Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string AmountText => $"+{Amount} XP";
}
