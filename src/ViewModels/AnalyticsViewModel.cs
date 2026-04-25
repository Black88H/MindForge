using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MindForge.ViewModels;

public partial class AnalyticsViewModel : ObservableObject
{
    [ObservableProperty] private string _activeTab = "XP";
    public List<string> Tabs { get; } = ["XP", "Streaks", "Fächer", "Zeit", "Achievements"];

    // Summary
    [ObservableProperty] private int _totalXP = 12450;
    [ObservableProperty] private int _level = 12;
    [ObservableProperty] private int _currentStreak = 12;
    [ObservableProperty] private double _overallSuccess = 0.82;
    [ObservableProperty] private int _achievementsUnlocked = 8;
    [ObservableProperty] private int _achievementsTotal = 14;

    public string OverallSuccessText => $"{OverallSuccess * 100:F0}%";
    public string AchievementText => $"{AchievementsUnlocked}/{AchievementsTotal}";

    // XP History (30 days)
    [ObservableProperty] private ObservableCollection<ChartPoint> _xpHistory = new(
        Enumerable.Range(0, 30).Select(i => new ChartPoint
        {
            Label = DateTime.Today.AddDays(i - 29).ToString("dd.MM"),
            Value = Random.Shared.Next(50, 500)
        }));

    // Streaks (14 days)
    [ObservableProperty] private ObservableCollection<ChartPoint> _streakHistory = new(
        Enumerable.Range(0, 14).Select(i => new ChartPoint
        {
            Label = DateTime.Today.AddDays(i - 13).ToString("dd.MM"),
            Value = Random.Shared.Next(0, 15)
        }));

    // Subjects
    [ObservableProperty] private ObservableCollection<SubjectStatItem> _subjectStats = new()
    {
        new() { Rank = "#1", Icon = "∫",   Name = "Analysis II",     SuccessRate = 0.81, Questions = 342, Level = 7  },
        new() { Rank = "#2", Icon = "En",  Name = "English C1",      SuccessRate = 0.94, Questions = 512, Level = 10 },
        new() { Rank = "#3", Icon = "{ }", Name = "Algorithmen",     SuccessRate = 0.79, Questions = 268, Level = 6  },
        new() { Rank = "#4", Icon = "ψ",   Name = "Quantenmechanik", SuccessRate = 0.73, Questions = 187, Level = 5  },
        new() { Rank = "#5", Icon = "◐",   Name = "Genetik",         SuccessRate = 0.71, Questions = 88,  Level = 3  },
        new() { Rank = "#6", Icon = "⌬",   Name = "Organ. Chemie",   SuccessRate = 0.67, Questions = 124, Level = 4  },
    };

    // Time tracking (pie)
    [ObservableProperty] private ObservableCollection<TimeItem> _timeTracking = new()
    {
        new() { SubjectName = "Analysis II",     Hours = 22.5, Color = "#5B8CFF" },
        new() { SubjectName = "English C1",      Hours = 18.2, Color = "#3fcf8e" },
        new() { SubjectName = "Algorithmen",     Hours = 14.7, Color = "#ff6b9d" },
        new() { SubjectName = "Quantenmechanik", Hours = 10.3, Color = "#BD93F9" },
        new() { SubjectName = "Organ. Chemie",   Hours = 6.8,  Color = "#ffb547" },
        new() { SubjectName = "Genetik",         Hours = 4.1,  Color = "#5eead4" },
    };

    public double TotalHours => TimeTracking.Sum(t => t.Hours);
    public string TotalHoursText => $"{TotalHours:F1} Std. gesamt";

    // Achievements
    [ObservableProperty] private ObservableCollection<AchievementBadge> _achievements = new()
    {
        new() { Name = "Erster Schritt",  Icon = "🥾", Rarity = "Häufig",   IsUnlocked = true  },
        new() { Name = "Wochenkrieger",   Icon = "⚔️",  Rarity = "Häufig",   IsUnlocked = true  },
        new() { Name = "Perfekte Zehn",   Icon = "💜",  Rarity = "Selten",   IsUnlocked = true  },
        new() { Name = "Nachteule",       Icon = "🦉",  Rarity = "Häufig",   IsUnlocked = true  },
        new() { Name = "Marathonläufer",  Icon = "🏃",  Rarity = "Selten",   IsUnlocked = false },
        new() { Name = "Meisterstudent",  Icon = "🎓",  Rarity = "Episch",   IsUnlocked = false },
        new() { Name = "Unsterblich",     Icon = "🔥",  Rarity = "Legendär", IsUnlocked = false },
        new() { Name = "Tausend Fragen",  Icon = "💯",  Rarity = "Episch",   IsUnlocked = false },
        new() { Name = "Schnelldenker",   Icon = "⚡",  Rarity = "Selten",   IsUnlocked = true  },
        new() { Name = "Analyse-Ass",     Icon = "📊",  Rarity = "Selten",   IsUnlocked = true  },
        new() { Name = "Planer",          Icon = "📅",  Rarity = "Häufig",   IsUnlocked = false },
        new() { Name = "OCR-Meister",     Icon = "🔍",  Rarity = "Selten",   IsUnlocked = false },
        new() { Name = "Challenger",      Icon = "🏆",  Rarity = "Häufig",   IsUnlocked = false },
        new() { Name = "Lernmaschine",    Icon = "🤖",  Rarity = "Episch",   IsUnlocked = false },
    };

    // Recommendations
    [ObservableProperty] private ObservableCollection<string> _recommendations = new()
    {
        "Du lernst Analysis am besten mit Active Recall — nutze mehr MC Tests!",
        "Dein Streak ist in Gefahr — lerne heute noch 10 Minuten.",
        "Du hast 13 Karten zur Wiederholung heute fällig.",
        "Erstelle einen Lernplan für Quantenmechanik — dein schwächstes Fach."
    };

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    [RelayCommand]
    private void RefreshCharts()
    {
        XpHistory = new ObservableCollection<ChartPoint>(
            Enumerable.Range(0, 30).Select(i => new ChartPoint
            {
                Label = DateTime.Today.AddDays(i - 29).ToString("dd.MM"),
                Value = Random.Shared.Next(50, 500)
            }));
    }
}

public class ChartPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public double MaxValue { get; set; } = 500;
    public double NormalizedHeight => Math.Clamp(Value / MaxValue, 0, 1) * 120;
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
}
