using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MindForge.ViewModels;

public partial class AnalyticsViewModel : ObservableObject
{
    [ObservableProperty] private string _activeTab = "XP Progress";
    [ObservableProperty] private int _totalXP = 2180;
    [ObservableProperty] private int _level = 12;
    [ObservableProperty] private int _xpToNextLevel = 2500;
    [ObservableProperty] private int _currentStreak = 12;
    [ObservableProperty] private int _bestStreak = 18;
    [ObservableProperty] private int _totalQuestions = 1521;
    [ObservableProperty] private double _overallSuccessRate = 0.816;

    public List<string> Tabs { get; } = ["XP Progress", "Streaks", "Fächer", "Zeit", "Achievements"];

    public List<ChartDataPoint> XPHistory { get; } = GenerateXPHistory();
    public List<ChartDataPoint> StreakHistory { get; } = GenerateStreakHistory();

    [ObservableProperty] private List<SubjectStatItem> _subjectStats = new()
    {
        new() { Rank=1, Name="English C1",     Icon="En", Color="#3fcf8e", SuccessRate=0.94, Questions=512, Level=8, XP=1240 },
        new() { Rank=2, Name="Analysis II",    Icon="∫",  Color="#5B8CFF", SuccessRate=0.81, Questions=342, Level=7, XP=980  },
        new() { Rank=3, Name="Algorithmen",    Icon="{}",  Color="#ff6b9d", SuccessRate=0.79, Questions=268, Level=6, XP=820  },
        new() { Rank=4, Name="Quantenmechanik",Icon="ψ",  Color="#BD93F9", SuccessRate=0.73, Questions=187, Level=5, XP=620  },
        new() { Rank=5, Name="Genetik",        Icon="◐",  Color="#5eead4", SuccessRate=0.71, Questions=88,  Level=3, XP=380  },
        new() { Rank=6, Name="Organ. Chemie",  Icon="⌬",  Color="#ffb547", SuccessRate=0.67, Questions=124, Level=4, XP=520  },
    };

    [ObservableProperty] private List<TimeStatItem> _timeStats = new()
    {
        new() { Subject="Analysis II",     Hours=12.5, Color="#5B8CFF", Percent=0.31 },
        new() { Subject="Algorithmen",     Hours=10.2, Color="#ff6b9d", Percent=0.25 },
        new() { Subject="English C1",      Hours=8.8,  Color="#3fcf8e", Percent=0.22 },
        new() { Subject="Quantenmechanik", Hours=5.1,  Color="#BD93F9", Percent=0.13 },
        new() { Subject="Organ. Chemie",   Hours=2.8,  Color="#ffb547", Percent=0.07 },
        new() { Subject="Genetik",         Hours=0.8,  Color="#5eead4", Percent=0.02 },
    };

    [ObservableProperty] private List<AchievementBadge> _achievements = new()
    {
        new() { Name="Erster Schritt",  Icon="🥾", Rarity="Häufig",   IsUnlocked=true  },
        new() { Name="Wochenkrieger",   Icon="⚔️",  Rarity="Häufig",   IsUnlocked=true  },
        new() { Name="Perfekte Zehn",   Icon="💜",  Rarity="Selten",   IsUnlocked=true  },
        new() { Name="Nachteule",       Icon="🦉",  Rarity="Häufig",   IsUnlocked=true  },
        new() { Name="Marathonläufer",  Icon="🏃",  Rarity="Selten",   IsUnlocked=false },
        new() { Name="Meisterstudent",  Icon="🎓",  Rarity="Episch",   IsUnlocked=false },
        new() { Name="Unsterblich",     Icon="🔥",  Rarity="Legendär", IsUnlocked=false },
        new() { Name="Wissenschaftler", Icon="🔬",  Rarity="Episch",   IsUnlocked=false },
        new() { Name="Schnelldenker",   Icon="⚡",  Rarity="Selten",   IsUnlocked=false },
        new() { Name="Tausend Fragen",  Icon="💯",  Rarity="Episch",   IsUnlocked=true  },
        new() { Name="Sprachgenie",     Icon="🗣️",  Rarity="Häufig",   IsUnlocked=false },
        new() { Name="Analyse-Ass",     Icon="📊",  Rarity="Selten",   IsUnlocked=true  },
    };

    public string OverallSuccessText => $"{OverallSuccessRate * 100:F1}%";
    public double XPProgress => XpToNextLevel > 0 ? (double)TotalXP / XpToNextLevel : 0;

    [RelayCommand]
    private void SelectTab(string tab) => ActiveTab = tab;

    private static List<ChartDataPoint> GenerateXPHistory()
    {
        var rng = new Random(42);
        return Enumerable.Range(0, 30).Select(i => new ChartDataPoint
        {
            Label = DateTime.Now.AddDays(-29 + i).ToString("dd.MM"),
            Value = 50 + rng.Next(0, 200) + i * 8,
        }).ToList();
    }

    private static List<ChartDataPoint> GenerateStreakHistory()
    {
        return Enumerable.Range(0, 14).Select(i => new ChartDataPoint
        {
            Label = DateTime.Now.AddDays(-13 + i).ToString("dd.MM"),
            Value = i < 12 ? i + 1 : 12,
        }).ToList();
    }
}

public class ChartDataPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
}

public class SubjectStatItem
{
    public int Rank { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = "#5B8CFF";
    public double SuccessRate { get; set; }
    public int Questions { get; set; }
    public int Level { get; set; }
    public int XP { get; set; }
    public string SuccessText => $"{SuccessRate * 100:F0}%";
}

public class TimeStatItem
{
    public string Subject { get; set; } = string.Empty;
    public double Hours { get; set; }
    public string Color { get; set; } = "#5B8CFF";
    public double Percent { get; set; }
    public string HoursText => $"{Hours:F1}h";
}
