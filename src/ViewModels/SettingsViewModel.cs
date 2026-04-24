using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    // Darstellung
    [ObservableProperty] private string _theme = "Dunkel";
    [ObservableProperty] private string _palette = "Forge (Default)";
    [ObservableProperty] private string _density = "Standard";
    [ObservableProperty] private string _fontFamily = "Geist";
    [ObservableProperty] private double _fontSize = 13.5;
    [ObservableProperty] private bool _sidebarCollapsed = false;
    [ObservableProperty] private bool _colorblindMode = false;
    [ObservableProperty] private bool _highContrast = false;
    [ObservableProperty] private bool _animationsEnabled = true;

    // KI & Provider
    [ObservableProperty] private string _claudeApiKey = string.Empty;
    [ObservableProperty] private string _openAiApiKey = string.Empty;
    [ObservableProperty] private string _geminiApiKey = string.Empty;
    [ObservableProperty] private string _ollamaEndpoint = "http://localhost:11434";
    [ObservableProperty] private string _defaultProvider = "Claude";
    [ObservableProperty] private int _tokenLimit = 4000;

    // Speicher
    [ObservableProperty] private string _databaseType = "SQLite";
    [ObservableProperty] private string _sqlitePath = "mindforge.db";
    [ObservableProperty] private string _sqlServerConnection = string.Empty;
    [ObservableProperty] private bool _autoBackup = true;
    [ObservableProperty] private string _backupFrequency = "Täglich";

    // Lernen
    [ObservableProperty] private string _defaultDifficulty = "Mittel";
    [ObservableProperty] private string _learningMethod = "Spaced Repetition";
    [ObservableProperty] private int _questionsPerSession = 20;
    [ObservableProperty] private bool _timeLimitEnabled = false;
    [ObservableProperty] private int _timeLimitSeconds = 30;

    // Gamification
    [ObservableProperty] private bool _xpEarningEnabled = true;
    [ObservableProperty] private bool _streakTrackingEnabled = true;
    [ObservableProperty] private bool _achievementsEnabled = true;
    [ObservableProperty] private double _xpMultiplier = 1.0;

    // Offline
    [ObservableProperty] private bool _offlineModeEnabled = false;
    [ObservableProperty] private bool _autoSync = true;
    [ObservableProperty] private string _offlineModel = "llama3";

    // Benachrichtigungen
    [ObservableProperty] private bool _notificationsEnabled = true;
    [ObservableProperty] private bool _soundEnabled = true;
    [ObservableProperty] private bool _dailyReminderEnabled = false;
    [ObservableProperty] private string _dailyReminderTime = "08:00";
    [ObservableProperty] private bool _streakNotificationsEnabled = true;

    // Datenschutz
    [ObservableProperty] private bool _analyticsSharing = false;
    [ObservableProperty] private bool _autoLogout = false;
    [ObservableProperty] private int _autoLogoutMinutes = 30;
    [ObservableProperty] private bool _dataEncryption = false;

    // Status
    [ObservableProperty] private string _saveStatus = string.Empty;
    [ObservableProperty] private bool _isSaving = false;

    public List<string> Themes { get; } = ["Dunkel", "Hell", "System"];
    public List<string> Palettes { get; } = ["Forge (Default)", "Material Blue", "Nord", "Dracula"];
    public List<string> Densities { get; } = ["Kompakt", "Standard", "Luftig"];
    public List<string> FontFamilies { get; } = ["Geist", "Segoe UI", "Calibri", "Consolas"];
    public List<string> Providers { get; } = ["Claude", "OpenAI", "Gemini", "Ollama (Lokal)"];
    public List<string> DbTypes { get; } = ["SQLite", "SQL Server"];
    public List<string> BackupFrequencies { get; } = ["Täglich", "Wöchentlich", "Monatlich"];
    public List<string> Difficulties { get; } = ["Leicht", "Mittel", "Schwer", "Gemischt"];
    public List<string> LearningMethods { get; } = ["Spaced Repetition", "Leitner-System", "Zufällig", "Schwächste zuerst"];
    public List<string> OfflineModels { get; } = ["llama3", "mistral", "phi3", "gemma"];
    public string XpMultiplierText => $"{XpMultiplier:F1}x";
    public string AppVersion => $"MindForge v{Constants.AppVersion}";

    public List<ShortcutItem> Shortcuts { get; } = new()
    {
        new() { Keys="Strg + K",         Action="Befehlspalette öffnen" },
        new() { Keys="Strg + 1",         Action="Lernmodus (Q&A)" },
        new() { Keys="Strg + 2",         Action="KI Content Generator" },
        new() { Keys="Strg + 3",         Action="Test erstellen" },
        new() { Keys="Strg + 4",         Action="Analytics" },
        new() { Keys="Strg + ,",         Action="Einstellungen" },
        new() { Keys="Strg + B",         Action="Sidebar ein-/ausklappen" },
        new() { Keys="Strg + Shift + T", Action="Theme wechseln" },
        new() { Keys="Enter",            Action="Antwort bestätigen" },
        new() { Keys="A / B / C / D",    Action="Antwort auswählen" },
    };

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        SaveStatus = string.Empty;
        await Task.Delay(500);
        var s = BuildSettings();
        Configuration.Save(s);
        IsSaving = false;
        SaveStatus = "✓ Einstellungen gespeichert";
        await Task.Delay(2000);
        SaveStatus = string.Empty;
    }

    [RelayCommand]
    private void BrowseSqlitePath()
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "SQLite (*.db)|*.db", FileName = SqlitePath };
        if (d.ShowDialog() == true) SqlitePath = d.FileName;
    }

    private AppSettings BuildSettings() => new()
    {
        Theme = Theme, Palette = Palette, Density = Density, FontFamily = FontFamily,
        FontSize = FontSize, SidebarCollapsed = SidebarCollapsed, ColorblindMode = ColorblindMode,
        HighContrast = HighContrast, AnimationsEnabled = AnimationsEnabled,
        ClaudeApiKey = ClaudeApiKey, OpenAiApiKey = OpenAiApiKey, GeminiApiKey = GeminiApiKey,
        OllamaEndpoint = OllamaEndpoint, DefaultProvider = DefaultProvider, TokenLimit = TokenLimit,
        DatabaseType = DatabaseType, SQLitePath = SqlitePath, SqlServerConnection = SqlServerConnection,
        AutoBackup = AutoBackup, BackupFrequency = BackupFrequency,
        DefaultDifficulty = DefaultDifficulty, LearningMethod = LearningMethod,
        QuestionsPerSession = QuestionsPerSession, TimeLimitEnabled = TimeLimitEnabled,
        TimeLimitSeconds = TimeLimitSeconds, XpEarningEnabled = XpEarningEnabled,
        StreakTrackingEnabled = StreakTrackingEnabled, AchievementsEnabled = AchievementsEnabled,
        XpMultiplier = XpMultiplier, OfflineModeEnabled = OfflineModeEnabled, AutoSync = AutoSync,
        OfflineModel = OfflineModel, NotificationsEnabled = NotificationsEnabled,
        SoundEnabled = SoundEnabled, DailyReminderEnabled = DailyReminderEnabled,
        DailyReminderTime = DailyReminderTime, StreakNotificationsEnabled = StreakNotificationsEnabled,
        AnalyticsSharing = AnalyticsSharing, AutoLogout = AutoLogout,
        AutoLogoutMinutes = AutoLogoutMinutes, DataEncryption = DataEncryption,
    };
}

public class ShortcutItem
{
    public string Keys { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
