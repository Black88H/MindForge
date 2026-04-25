using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MindForge.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    // User info
    [ObservableProperty] private string _userName = "Jonas";
    [ObservableProperty] private string _email = "jonas@example.com";
    [ObservableProperty] private string _avatarInitials = "JW";
    [ObservableProperty] private bool _isEditing = false;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editEmail = string.Empty;

    // Stats
    [ObservableProperty] private int _totalXP = 12450;
    [ObservableProperty] private int _level = 12;
    [ObservableProperty] private int _xpToNextLevel = 15000;
    [ObservableProperty] private int _longestStreak = 18;
    [ObservableProperty] private int _currentStreak = 12;
    [ObservableProperty] private int _achievementsUnlocked = 8;
    [ObservableProperty] private int _achievementsTotal = 14;
    [ObservableProperty] private int _totalQuestionsAnswered = 2847;
    [ObservableProperty] private double _overallSuccessRate = 0.82;

    public double XPProgress => XpToNextLevel > 0 ? Math.Clamp((double)TotalXP / XpToNextLevel, 0, 1) : 0;
    public string XPProgressText => $"{TotalXP} / {XpToNextLevel} XP";
    public string OverallSuccessText => $"{OverallSuccessRate * 100:F0}%";

    // Learning profile
    [ObservableProperty] private ObservableCollection<string> _preferredMethods = new()
        { "Active Recall", "Spaced Repetition" };
    [ObservableProperty] private string _learningStyle = "Mixed";

    // Best subjects
    [ObservableProperty] private ObservableCollection<SubjectViewModel> _bestSubjects = new()
    {
        new() { Name = "English C1",  Icon = "En",   Color = "#3fcf8e", SuccessRate = 0.94, Progress = 0.91 },
        new() { Name = "Analysis II", Icon = "∫",    Color = "#5B8CFF", SuccessRate = 0.81, Progress = 0.68 },
        new() { Name = "Algorithmen", Icon = "{ }",  Color = "#ff6b9d", SuccessRate = 0.79, Progress = 0.57 },
    };

    // Recent achievements
    [ObservableProperty] private ObservableCollection<AchievementBadge> _recentAchievements = new()
    {
        new() { Name = "Erster Schritt", Icon = "🥾", Rarity = "Häufig",  IsUnlocked = true },
        new() { Name = "Wochenkrieger",  Icon = "⚔️",  Rarity = "Häufig",  IsUnlocked = true },
        new() { Name = "Perfekte Zehn",  Icon = "💜",  Rarity = "Selten",  IsUnlocked = true },
        new() { Name = "Schnelldenker",  Icon = "⚡",  Rarity = "Selten",  IsUnlocked = true },
    };

    [RelayCommand]
    private void StartEdit()
    {
        EditName = UserName;
        EditEmail = Email;
        IsEditing = true;
    }

    [RelayCommand]
    private void SaveProfile()
    {
        if (!string.IsNullOrWhiteSpace(EditName)) UserName = EditName;
        if (!string.IsNullOrWhiteSpace(EditEmail)) Email = EditEmail;
        AvatarInitials = UserName.Length >= 2
            ? $"{UserName[0]}{UserName[^1]}".ToUpper()
            : UserName.ToUpper();
        IsEditing = false;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;
}
