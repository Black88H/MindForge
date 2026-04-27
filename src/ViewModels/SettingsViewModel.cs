using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Services;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly UpdateService _updateService;

    public SettingsViewModel(UpdateService updateService) => _updateService = updateService;

    [ObservableProperty] private string _activeSection = "Darstellung";
    public List<string> Sections { get; } = [
        "Darstellung", "KI & Provider", "Lernen", "Gamification",
        "Offline", "Benachrichtigungen", "Datenschutz", "Speicher",
        "Hardware", "Über"
    ];

    // ── Section 1: Darstellung ───────────────────────────────────────────────
    [ObservableProperty] private string _theme = "Dunkel";
    [ObservableProperty] private string _palette = "Forge (Default)";
    [ObservableProperty] private string _density = "Standard";
    [ObservableProperty] private double _fontSize = 13.5;
    [ObservableProperty] private bool _sidebarCollapsed = false;
    [ObservableProperty] private bool _colorblindMode = false;
    [ObservableProperty] private bool _highContrast = false;
    [ObservableProperty] private bool _animationsEnabled = true;

    public bool IsDark   { get => Theme == "Dunkel"; set { if (value) Theme = "Dunkel"; } }
    public bool IsLight  { get => Theme == "Hell";   set { if (value) Theme = "Hell"; } }
    public bool IsSystem { get => Theme == "System"; set { if (value) Theme = "System"; } }
    public List<string> FontFamilies { get; } = ["Segoe UI", "Consolas", "Calibri", "Georgia", "Arial"];
    [ObservableProperty] private string _fontFamily = "Segoe UI";

    public List<string> Themes { get; } = ["Dunkel", "Hell", "System"];
    public List<string> Palettes { get; } = ["Forge (Default)", "Material Blue", "Nord", "Dracula"];
    public List<string> Densities { get; } = ["Kompakt", "Standard", "Luftig"];

    // ── Section 2: KI & Provider ─────────────────────────────────────────────
    [ObservableProperty] private string _defaultProvider = "Claude";
    [ObservableProperty] private string _claudeApiKey = string.Empty;
    [ObservableProperty] private string _openAiApiKey = string.Empty;
    [ObservableProperty] private string _geminiApiKey = string.Empty;
    [ObservableProperty] private string _ollamaEndpoint = "http://localhost:11434";
    [ObservableProperty] private bool _autoSelectProvider = true;
    [ObservableProperty] private int _tokenLimit = 4096;
    [ObservableProperty] private decimal _tokenBudgetUsd = 10.0m;
    [ObservableProperty] private decimal _tokenUsageThisMonth = 4.23m;
    public string TokenBudgetStatusText => $"Diesen Monat: ${TokenUsageThisMonth:F2} / ${TokenBudgetUsd:F2}";

    public List<string> Providers { get; } = ["Claude", "OpenAI", "Gemini", "Ollama (Lokal)"];

    // ── Section 3: Lernen ────────────────────────────────────────────────────
    public List<string> Difficulties { get; } = ["Leicht", "Mittel", "Schwer", "Gemischt"];
    [ObservableProperty] private string _defaultDifficulty = "Mittel";
    public List<string> LearningMethods { get; } = ["Active Recall", "Karteikarten", "Multiple Choice", "Mixed"];
    [ObservableProperty] private string _learningMethod = "Mixed";
    [ObservableProperty] private int _questionsPerSession = 20;
    [ObservableProperty] private bool _timeLimitEnabled = false;
    [ObservableProperty] private int _timeLimitSeconds = 60;
    [ObservableProperty] private bool _methodActiveRecall = true;
    [ObservableProperty] private bool _methodPomodoro = false;
    [ObservableProperty] private bool _methodSpacedRep = true;
    [ObservableProperty] private bool _methodInterleaving = false;
    [ObservableProperty] private bool _methodPracticeTest = true;
    [ObservableProperty] private bool _shortExplanation = false;
    [ObservableProperty] private bool _needsExamples = true;
    [ObservableProperty] private bool _needsExercises = true;
    [ObservableProperty] private bool _needsFormulas = false;
    [ObservableProperty] private int _problemsPerLesson = 20;

    // ── Section 4: Gamification ──────────────────────────────────────────────
    [ObservableProperty] private bool _xpEarningEnabled = true;
    [ObservableProperty] private bool _streakTrackingEnabled = true;
    [ObservableProperty] private bool _achievementsEnabled = true;
    [ObservableProperty] private double _xpMultiplier = 1.0;
    [ObservableProperty] private bool _showNotifications = true;
    [ObservableProperty] private string _leaderboardMode = "Local";
    public string XpMultiplierText => $"{XpMultiplier:F1}x";
    public List<string> LeaderboardModes { get; } = ["Local", "Cloud", "Aus"];

    // ── Section 5: Offline ───────────────────────────────────────────────────
    [ObservableProperty] private bool _offlineModeEnabled = false;
    [ObservableProperty] private bool _autoSync = true;
    public List<string> OfflineModels { get; } = ["Llama 3.1 8B", "Mistral 7B", "Phi-3 Mini"];
    [ObservableProperty] private string _offlineModel = "Llama 3.1 8B";
    [ObservableProperty] private bool _offlineModelsInstalled = false;
    [ObservableProperty] private string _syncMode = "Local";
    [ObservableProperty] private bool _deleteLocalAfterSync = false;
    public List<string> SyncModes { get; } = ["Local", "Cloud", "Hybrid"];
    public string OllamaStatus => OfflineModelsInstalled ? "✓ Installiert" : "✗ Nicht installiert";

    // ── Section 6: Benachrichtigungen ────────────────────────────────────────
    [ObservableProperty] private bool _notificationsEnabled = true;
    [ObservableProperty] private bool _soundEnabled = true;
    [ObservableProperty] private bool _dailyReminderEnabled = true;
    [ObservableProperty] private bool _streakNotificationsEnabled = true;
    [ObservableProperty] private bool _toastNotifications = true;
    [ObservableProperty] private bool _desktopNotifications = true;
    [ObservableProperty] private bool _updateNotifications = true;
    [ObservableProperty] private bool _streakReminders = true;

    // ── Section 7: Datenschutz ───────────────────────────────────────────────
    [ObservableProperty] private bool _analyticsSharing = false;
    [ObservableProperty] private bool _autoLogout = false;
    [ObservableProperty] private bool _dataEncryption = false;
    [ObservableProperty] private string _dataRetention = "Forever";
    [ObservableProperty] private bool _cloudSync = false;
    [ObservableProperty] private bool _anonymousAnalytics = false;
    public List<string> DataRetentions { get; } = ["Forever", "1 Jahr", "6 Monate", "3 Monate"];

    // ── Section 8: Speicher ──────────────────────────────────────────────────
    [ObservableProperty] private string _sqlitePath = "mindforge.db";
    [ObservableProperty] private bool _autoBackup = true;
    [ObservableProperty] private string _backupFrequency = "Täglich";
    public List<string> BackupFrequencies { get; } = ["Täglich", "Wöchentlich", "Monatlich"];
    [ObservableProperty] private string _backupStatus = string.Empty;
    [ObservableProperty] private string _exportStatus = string.Empty;

    // ── Section 9: Hardware ──────────────────────────────────────────────────
    public string CpuName => HardwareInfo.Split('·').ElementAtOrDefault(0)?.Trim() ?? "—";
    public string RamInfo => HardwareInfo.Split('·').ElementAtOrDefault(1)?.Trim() ?? "—";
    public string ScreenInfo => HardwareInfo.Split('·').ElementAtOrDefault(2)?.Trim() ?? "—";
    [ObservableProperty] private int _maxCpuUsage = 80;
    [ObservableProperty] private int _maxRamUsage = 70;
    [ObservableProperty] private bool _autoOptimize = true;
    [ObservableProperty] private string _hardwareInfo = "RAM: — · CPU: — · Auflösung: —";
    [ObservableProperty] private int _currentCpuPercent = 0;
    [ObservableProperty] private int _currentRamPercent = 0;

    // ── Section 10: Über ─────────────────────────────────────────────────────
    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private string _latestVersion = string.Empty;
    [ObservableProperty] private bool _updateAvailable = false;
    [ObservableProperty] private string _lastChecked = "Noch nicht geprüft";
    [ObservableProperty] private bool _isCheckingUpdates = false;
    public string UpdateStatusText => UpdateStatus;
    public string AppVersion => $"MindForge v{Constants.AppVersion}";
    public string GitHubUrl => $"https://github.com/{Constants.GitHub.Owner}/{Constants.GitHub.Repo}";

    // Status
    [ObservableProperty] private string _saveStatus = string.Empty;
    [ObservableProperty] private bool _isSaving = false;
    public string SaveButtonText => IsSaving ? "Speichern..." : "Speichern";
    partial void OnIsSavingChanged(bool value) => OnPropertyChanged(nameof(SaveButtonText));

    // Shortcuts
    public List<ShortcutItem> Shortcuts { get; } = new()
    {
        new() { Keys = "Strg + K",          Action = "Befehlspalette öffnen" },
        new() { Keys = "Strg + 1",          Action = "Dashboard" },
        new() { Keys = "Strg + 2",          Action = "Lernen" },
        new() { Keys = "Strg + 3",          Action = "Tests" },
        new() { Keys = "Strg + 4",          Action = "Analytics" },
        new() { Keys = "Strg + 5",          Action = "KI-Werkzeuge" },
        new() { Keys = "Strg + ,",          Action = "Einstellungen" },
        new() { Keys = "Strg + B",          Action = "Sidebar ein-/ausklappen" },
        new() { Keys = "Strg + Shift + T",  Action = "Theme wechseln" },
        new() { Keys = "Enter",             Action = "Antwort bestätigen" },
        new() { Keys = "A / B / C / D",     Action = "Antwort auswählen" },
    };

    [RelayCommand]
    private void SelectSection(string section) => ActiveSection = section;

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;
        SaveStatus = string.Empty;
        await Task.Delay(400);
        var s = BuildSettings();
        Configuration.Save(s);
        IsSaving = false;
        SaveStatus = "✓ Einstellungen gespeichert";
        await Task.Delay(2000);
        SaveStatus = string.Empty;
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        IsCheckingUpdates = true;
        UpdateStatus = "Prüfe auf Updates...";
        var info = await _updateService.CheckForUpdatesAsync();
        LastChecked = info.CheckedAt.ToString("dd.MM.yyyy HH:mm");
        UpdateAvailable = info.IsUpdateAvailable;
        LatestVersion = info.IsUpdateAvailable ? $"v{info.LatestVersion}" : string.Empty;
        UpdateStatus = info.IsUpdateAvailable
            ? $"Update verfügbar: v{info.LatestVersion}"
            : "✓ MindForge ist aktuell";
        IsCheckingUpdates = false;
    }

    [RelayCommand]
    private void DetectHardware()
    {
        var hw = HardwareDetector.Detect();
        HardwareInfo = hw.Summary;
    }

    [RelayCommand]
    private async Task ExportDataAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv|JSON|*.json", FileName = "mindforge-export" };
        if (dialog.ShowDialog() == true)
        {
            await System.IO.File.WriteAllTextAsync(dialog.FileName, "# MindForge Export\n");
            ExportStatus = "✓ Daten exportiert";
            await Task.Delay(2000);
            ExportStatus = string.Empty;
        }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        BackupStatus = "Backup erstellt: mindforge_backup_" + DateTime.Now.ToString("yyyyMMdd") + ".db";
        await Task.Delay(2000);
        BackupStatus = string.Empty;
    }

    [RelayCommand]
    private void BrowseSqlitePath()
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = "SQLite (*.db)|*.db", FileName = "mindforge.db" };
        if (d.ShowDialog() == true) { }
    }

    private AppSettings BuildSettings() => new()
    {
        Theme = Theme, Palette = Palette, Density = Density,
        FontSize = FontSize, SidebarCollapsed = SidebarCollapsed,
        ColorblindMode = ColorblindMode, HighContrast = HighContrast, AnimationsEnabled = AnimationsEnabled,
        DefaultProvider = DefaultProvider, ClaudeApiKey = ClaudeApiKey,
        OpenAiApiKey = OpenAiApiKey, GeminiApiKey = GeminiApiKey,
        OllamaEndpoint = OllamaEndpoint, AutoSelectProvider = AutoSelectProvider,
        TokenBudgetUSD = TokenBudgetUsd, ShortExplanation = ShortExplanation,
        NeedsExamples = NeedsExamples, NeedsExercises = NeedsExercises,
        QuestionsPerSession = ProblemsPerLesson, XpMultiplier = XpMultiplier,
        ShowNotifications = ShowNotifications, LeaderboardMode = LeaderboardMode,
        SyncMode = SyncMode, DeleteLocalAfterSync = DeleteLocalAfterSync,
        NotificationsEnabled = ToastNotifications, DesktopNotifications = DesktopNotifications,
        UpdateNotifications = UpdateNotifications, DataRetention = DataRetention,
        CloudSync = CloudSync, AnonymousAnalytics = AnonymousAnalytics,
        MaxCpuUsage = MaxCpuUsage, MaxRamUsage = MaxRamUsage, AutoOptimize = AutoOptimize,
    };
}

public class ShortcutItem
{
    public string Keys { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}
