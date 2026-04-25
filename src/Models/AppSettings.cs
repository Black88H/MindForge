namespace MindForge.Models;

public class AppSettings
{
    // Darstellung
    public string Theme { get; set; } = "Dunkel";
    public string Palette { get; set; } = "Forge";
    public string Density { get; set; } = "Standard";
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 13.5;
    public bool SidebarCollapsed { get; set; } = false;
    public bool ColorblindMode { get; set; } = false;
    public bool HighContrast { get; set; } = false;
    public bool AnimationsEnabled { get; set; } = true;

    // KI & Provider
    public string ClaudeApiKey { get; set; } = string.Empty;
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string GeminiApiKey { get; set; } = string.Empty;
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string DefaultProvider { get; set; } = "Claude";
    public int TokenLimit { get; set; } = 4000;
    public bool AutoSelectProvider { get; set; } = true;
    public decimal TokenBudgetUSD { get; set; } = 5.0m;

    // Speicher & Datenbank
    public string DatabaseType { get; set; } = "SQLite";
    public string SQLitePath { get; set; } = "mindforge.db";
    public string SqlServerConnection { get; set; } = string.Empty;
    public bool AutoBackup { get; set; } = true;
    public string BackupFrequency { get; set; } = "Täglich";

    // Lernen
    public string DefaultDifficulty { get; set; } = "Mittel";
    public string LearningMethod { get; set; } = "Spaced Repetition";
    public int QuestionsPerSession { get; set; } = 20;
    public bool TimeLimitEnabled { get; set; } = false;
    public int TimeLimitSeconds { get; set; } = 30;
    public bool ShortExplanation { get; set; } = false;
    public bool NeedsExamples { get; set; } = true;
    public bool NeedsExercises { get; set; } = true;

    // Gamification
    public bool XpEarningEnabled { get; set; } = true;
    public bool StreakTrackingEnabled { get; set; } = true;
    public bool AchievementsEnabled { get; set; } = true;
    public double XpMultiplier { get; set; } = 1.0;
    public bool ShowNotifications { get; set; } = true;
    public string LeaderboardMode { get; set; } = "Local";

    // Offline
    public bool OfflineModeEnabled { get; set; } = false;
    public bool AutoSync { get; set; } = true;
    public string OfflineModel { get; set; } = "llama3";
    public string SyncMode { get; set; } = "Local";
    public bool DeleteLocalAfterSync { get; set; } = false;

    // Benachrichtigungen
    public bool NotificationsEnabled { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool DailyReminderEnabled { get; set; } = false;
    public string DailyReminderTime { get; set; } = "08:00";
    public bool StreakNotificationsEnabled { get; set; } = true;
    public bool DesktopNotifications { get; set; } = true;
    public bool UpdateNotifications { get; set; } = true;

    // Datenschutz
    public bool AnalyticsSharing { get; set; } = false;
    public bool AutoLogout { get; set; } = false;
    public int AutoLogoutMinutes { get; set; } = 30;
    public bool DataEncryption { get; set; } = false;
    public string DataRetention { get; set; } = "Forever";
    public bool CloudSync { get; set; } = false;
    public bool AnonymousAnalytics { get; set; } = false;

    // Hardware
    public int MaxCpuUsage { get; set; } = 80;
    public int MaxRamUsage { get; set; } = 70;
    public bool AutoOptimize { get; set; } = true;
}
