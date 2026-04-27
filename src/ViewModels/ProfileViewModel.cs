using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly MindForgeDbContext _db;
    private readonly UserProgressRepository _progress;
    private readonly AchievementRepository _achievements;
    private readonly AnalyticsRepository _analytics;

    public ProfileViewModel(
        MindForgeDbContext db,
        UserProgressRepository progress,
        AchievementRepository achievements,
        AnalyticsRepository analytics)
    {
        _db = db;
        _progress = progress;
        _achievements = achievements;
        _analytics = analytics;
        _ = LoadAsync();
    }

    // User info — sourced from UserSession at construction
    [ObservableProperty] private string _userName = UserSession.Username;
    [ObservableProperty] private string _email = UserSession.Email;
    [ObservableProperty] private string _avatarInitials = ComputeInitials(UserSession.Username);
    [ObservableProperty] private bool _isEditing = false;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editEmail = string.Empty;

    public string EditUserName { get => EditName; set => EditName = value; }
    partial void OnEditNameChanged(string value) => OnPropertyChanged(nameof(EditUserName));

    // Stats
    [ObservableProperty] private int _totalXP = UserSession.TotalXP;
    [ObservableProperty] private int _level = UserSession.Level;
    [ObservableProperty] private int _xpToNextLevel = 100;
    [ObservableProperty] private int _longestStreak = UserSession.LongestStreak;
    [ObservableProperty] private int _currentStreak = UserSession.CurrentStreak;
    [ObservableProperty] private int _achievementsUnlocked = 0;
    [ObservableProperty] private int _achievementsTotal = 0;
    [ObservableProperty] private int _totalQuestionsAnswered = 0;
    [ObservableProperty] private double _overallSuccessRate = 0;

    public int NextLevel => Level + 1;
    public double XPProgress => XpToNextLevel > 0 ? Math.Clamp((double)TotalXP / XpToNextLevel, 0, 1) : 0;
    public string XPProgressText => $"{TotalXP} / {XpToNextLevel} XP";
    public string OverallSuccessText => $"{OverallSuccessRate * 100:F0}%";

    [ObservableProperty] private ObservableCollection<string> _preferredMethods = new();
    [ObservableProperty] private string _learningStyle = "Mixed";

    [ObservableProperty] private ObservableCollection<SubjectViewModel> _bestSubjects = new();
    [ObservableProperty] private ObservableCollection<AchievementBadge> _recentAchievements = new();

    public bool HasBestSubjects => BestSubjects.Count > 0;
    public bool HasAchievements => RecentAchievements.Count > 0;

    partial void OnBestSubjectsChanged(ObservableCollection<SubjectViewModel> value) => OnPropertyChanged(nameof(HasBestSubjects));
    partial void OnRecentAchievementsChanged(ObservableCollection<AchievementBadge> value) => OnPropertyChanged(nameof(HasAchievements));
    partial void OnTotalXPChanged(int value) => OnPropertyChanged(nameof(XPProgressText));
    partial void OnXpToNextLevelChanged(int value) => OnPropertyChanged(nameof(XPProgressText));
    partial void OnOverallSuccessRateChanged(double value) => OnPropertyChanged(nameof(OverallSuccessText));

    private async Task LoadAsync()
    {
        // XP target for next level — same formula as GamificationService
        XpToNextLevel = (int)Math.Pow(Level, 2) * 50;

        TotalQuestionsAnswered = await _analytics.GetTotalQuestionsAnsweredAsync();
        OverallSuccessRate     = await _analytics.GetOverallSuccessRateAsync();

        // Achievements
        var achievements = (await _achievements.GetAchievementsAsync()).ToList();
        AchievementsTotal    = achievements.Count;
        AchievementsUnlocked = achievements.Count(a => a.IsUnlocked);

        // Show 4 most-recently unlocked achievements (or first 4 if none unlocked)
        var recent = achievements
            .Where(a => a.IsUnlocked)
            .OrderByDescending(a => a.UnlockedAt ?? DateTime.MinValue)
            .Take(4)
            .Concat(achievements.Where(a => !a.IsUnlocked).Take(4))
            .Take(4)
            .Select(a => new AchievementBadge
            {
                Name = a.Name,
                Icon = a.Icon,
                Rarity = a.Rarity.ToString(),
                Description = a.Description,
                IsUnlocked = a.IsUnlocked,
            });
        RecentAchievements = new ObservableCollection<AchievementBadge>(recent);

        // Best subjects: top 3 by SuccessRate
        var subjects = await _db.Subjects
            .OrderByDescending(s => s.SuccessRate)
            .ThenByDescending(s => s.Progress)
            .Take(3)
            .ToListAsync();
        BestSubjects = new ObservableCollection<SubjectViewModel>(subjects.Select(s => new SubjectViewModel
        {
            Id = s.Id,
            Name = s.Name,
            Icon = s.Icon,
            Color = s.Color,
            Progress = s.Progress,
            SuccessRate = s.SuccessRate,
            QuestionCount = s.QuestionCount,
        }));
    }

    private static string ComputeInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        return name.Length >= 2
            ? $"{char.ToUpper(name[0])}{char.ToUpper(name[^1])}"
            : char.ToUpper(name[0]).ToString();
    }

    [RelayCommand]
    private void StartEdit()
    {
        EditName = UserName;
        EditEmail = Email;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        if (UserSession.UserId == Guid.Empty) { IsEditing = false; return; }

        var user = await _db.Users.FindAsync(UserSession.UserId);
        if (user == null) { IsEditing = false; return; }

        if (!string.IsNullOrWhiteSpace(EditName)) user.Username = EditName;
        if (!string.IsNullOrWhiteSpace(EditEmail)) user.Email   = EditEmail;
        await _db.SaveChangesAsync();

        // Reflect in VM and session
        UserName        = user.Username;
        Email           = user.Email;
        AvatarInitials  = ComputeInitials(user.Username);
        UserSession.Login(user); // refresh static session
        IsEditing       = false;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;
}
