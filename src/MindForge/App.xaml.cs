using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Services;
using MindForge.Services.AI;
using MindForge.Services.AI.Providers;
using MindForge.Services.Interfaces;
using MindForge.ViewModels;
using MindForge.Views;
using System.Windows;
using System;
using System.IO;

namespace MindForge;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private static readonly string ErrorLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MindForge", "error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        // Global unhandled-exception handlers — prevent silent crashes
        DispatcherUnhandledException += (_, args) =>
        {
            LogError(args.Exception);
            System.Windows.MessageBox.Show(
                $"Ein unerwarteter Fehler ist aufgetreten:\n\n{args.Exception.Message}\n\n" +
                $"Details wurden gespeichert unter:\n{ErrorLogPath}",
                "MindForge – Fehler", System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                LogError(ex);
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogError(args.Exception);
            args.SetObserved();
        };
        var services = new ServiceCollection();

        // Database — explicit absolute path so it always lands in AppData\Local\MindForge
        // regardless of CWD, single-file publish location, or working directory.
        var dbDir  = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MindForge");
        var dbPath = Path.Combine(dbDir, "mindforge.db");
        Directory.CreateDirectory(dbDir);

        services.AddDbContext<MindForgeDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // AI layer
        services.AddSingleton<OllamaProvider>();
        services.AddSingleton<AISelector>();
        services.AddSingleton<RAGService>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAPIKeyService, APIKeyService>();
        services.AddScoped<IFileIngestionService, FileIngestionService>();
        services.AddScoped<IKnowledgeGraphService, KnowledgeGraphService>();
        services.AddScoped<ILearningPlanService, LearningPlanService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ITestService, TestService>();
        services.AddScoped<IGamificationService, GamificationService>();
        services.AddScoped<ISpacedRepetitionService, SpacedRepetitionService>();
        services.AddScoped<INotebookService, NotebookService>();

        // ── v8.0.0 services ──────────────────────────────────────────────────
        services.AddScoped<IGlobalSearchService, GlobalSearchService>();
        services.AddScoped<IAdaptiveQuizService, AdaptiveQuizService>();
        services.AddScoped<IAnalyticsService,    AnalyticsService>();
        services.AddScoped<INotebookExportService, NotebookExportService>();
        // Singletons — timer and voice hold device state
        services.AddSingleton<IStudyTimerService, StudyTimerService>();
        services.AddSingleton<IVoiceInputService, VoiceInputService>();

        // Background task service — Singleton; uses IServiceScopeFactory for DB access
        services.AddSingleton<IBackgroundTaskService, BackgroundTaskService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<SubjectsViewModel>();
        services.AddTransient<MaterialLibraryViewModel>();
        services.AddTransient<KnowledgeGraphViewModel>();
        services.AddTransient<LearningPlanViewModel>();
        services.AddTransient<LearningViewModel>();
        services.AddTransient<ChatViewModel>();
        services.AddTransient<TestsViewModel>();
        services.AddTransient<KIToolsViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<AnalyticsViewModel>();

        Services = services.BuildServiceProvider();

        // Ensure database schema is up to date.
        // EnsureCreated covers fresh installs; the raw SQL block handles existing DBs
        // where EnsureCreated skips (it only creates if the DB didn't exist yet).
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MindForgeDbContext>();
            db.Database.EnsureCreated();

            // ── Schema additions for v3.5.0 (safe on existing and fresh DBs) ──────
            // CREATE TABLE IF NOT EXISTS never fails on repeat; ALTER TABLE ADD COLUMN
            // fails silently if the column already exists — we catch and ignore.
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Notebooks (
                    Id               TEXT NOT NULL PRIMARY KEY,
                    SubjectId        TEXT NOT NULL,
                    UserId           TEXT NOT NULL,
                    Name             TEXT NOT NULL DEFAULT '',
                    LearningLevel    TEXT NOT NULL DEFAULT 'Fortgeschritten',
                    ExplanationStyle TEXT NOT NULL DEFAULT 'Normal',
                    Progress         REAL NOT NULL DEFAULT 0,
                    ChatCount        INTEGER NOT NULL DEFAULT 0,
                    CreatedAt        TEXT NOT NULL DEFAULT '',
                    LastModified     TEXT NOT NULL DEFAULT ''
                );");

            try { db.Database.ExecuteSqlRaw("ALTER TABLE Materials    ADD COLUMN NotebookId TEXT NULL;"); } catch { /* column already exists */ }
            try { db.Database.ExecuteSqlRaw("ALTER TABLE ChatMessages ADD COLUMN NotebookId TEXT NULL;"); } catch { /* column already exists */ }
            try { db.Database.ExecuteSqlRaw("ALTER TABLE Notebooks    ADD COLUMN Language TEXT NOT NULL DEFAULT 'Deutsch';"); } catch { /* column already exists */ }

            // ── v7.0.0 schema additions ───────────────────────────────────────────
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS BackgroundTaskRecords (
                    Id          TEXT NOT NULL PRIMARY KEY,
                    TaskName    TEXT NOT NULL DEFAULT '',
                    Status      TEXT NOT NULL DEFAULT '',
                    Result      TEXT NOT NULL DEFAULT '',
                    Error       TEXT NOT NULL DEFAULT '',
                    StartedAt   TEXT NOT NULL DEFAULT '',
                    CompletedAt TEXT NULL
                );");

            // ── v4.0.0 schema additions ───────────────────────────────────────────
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS MaterialChunks (
                    Id            TEXT NOT NULL PRIMARY KEY,
                    MaterialId    TEXT NOT NULL,
                    NotebookId    TEXT NOT NULL,
                    MaterialName  TEXT NOT NULL DEFAULT '',
                    ChunkIndex    INTEGER NOT NULL DEFAULT 0,
                    Text          TEXT NOT NULL DEFAULT '',
                    EmbeddingJson TEXT NOT NULL DEFAULT '[]'
                );");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Formulas (
                    Id          TEXT NOT NULL PRIMARY KEY,
                    NotebookId  TEXT NOT NULL,
                    LaTeX       TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    Category    TEXT NOT NULL DEFAULT '',
                    CreatedAt   TEXT NOT NULL DEFAULT ''
                );");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS NotebookSnapshots (
                    Id            TEXT NOT NULL PRIMARY KEY,
                    NotebookId    TEXT NOT NULL,
                    CreatedAt     TEXT NOT NULL DEFAULT '',
                    Label         TEXT NOT NULL DEFAULT '',
                    MaterialsJson TEXT NOT NULL DEFAULT '[]',
                    ChatJson      TEXT NOT NULL DEFAULT '[]',
                    MaterialCount INTEGER NOT NULL DEFAULT 0,
                    ChatCount     INTEGER NOT NULL DEFAULT 0
                );");

            // ── v8.0.0 schema additions ──────────────────────────────────────────
            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS Tags (
                    Id     TEXT NOT NULL PRIMARY KEY,
                    UserId TEXT NOT NULL DEFAULT '',
                    Name   TEXT NOT NULL DEFAULT '',
                    Color  TEXT NOT NULL DEFAULT '#6366F1'
                );");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS NotebookTags (
                    Id         TEXT NOT NULL PRIMARY KEY,
                    NotebookId TEXT NOT NULL DEFAULT '',
                    TagId      TEXT NOT NULL DEFAULT ''
                );");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS SearchIndexes (
                    Id         TEXT NOT NULL PRIMARY KEY,
                    EntityType TEXT NOT NULL DEFAULT '',
                    EntityId   TEXT NOT NULL DEFAULT '',
                    Title      TEXT NOT NULL DEFAULT '',
                    Snippet    TEXT NOT NULL DEFAULT '',
                    UserId     TEXT NOT NULL DEFAULT '',
                    IndexedAt  TEXT NOT NULL DEFAULT ''
                );");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS StudyStatistics (
                    Id                 TEXT NOT NULL PRIMARY KEY,
                    UserId             TEXT NOT NULL DEFAULT '',
                    Date               TEXT NOT NULL DEFAULT '',
                    MinutesStudied     INTEGER NOT NULL DEFAULT 0,
                    SessionCount       INTEGER NOT NULL DEFAULT 0,
                    XPEarned           INTEGER NOT NULL DEFAULT 0,
                    FlashcardsReviewed INTEGER NOT NULL DEFAULT 0,
                    TestsTaken         INTEGER NOT NULL DEFAULT 0,
                    AverageScore       REAL NOT NULL DEFAULT 0,
                    QuizzesTaken       INTEGER NOT NULL DEFAULT 0,
                    ChatMessages       INTEGER NOT NULL DEFAULT 0
                );");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS StudySessions (
                    Id              TEXT NOT NULL PRIMARY KEY,
                    UserId          TEXT NOT NULL DEFAULT '',
                    NotebookId      TEXT NULL,
                    SessionType     TEXT NOT NULL DEFAULT 'Pomodoro',
                    StartedAt       TEXT NOT NULL DEFAULT '',
                    EndedAt         TEXT NULL,
                    DurationMinutes INTEGER NOT NULL DEFAULT 0,
                    Completed       INTEGER NOT NULL DEFAULT 0,
                    XPEarned        INTEGER NOT NULL DEFAULT 0
                );");

            db.Database.ExecuteSqlRaw(@"
                CREATE TABLE IF NOT EXISTS TokenUsages (
                    Id               TEXT NOT NULL PRIMARY KEY,
                    UserId           TEXT NOT NULL DEFAULT '',
                    NotebookId       TEXT NULL,
                    Provider         TEXT NOT NULL DEFAULT '',
                    Model            TEXT NOT NULL DEFAULT '',
                    PromptTokens     INTEGER NOT NULL DEFAULT 0,
                    CompletionTokens INTEGER NOT NULL DEFAULT 0,
                    Feature          TEXT NOT NULL DEFAULT '',
                    Timestamp        TEXT NOT NULL DEFAULT ''
                );");
        } // end using scope

        // Load saved Ollama URL into selector
        LoadOllamaSettings(Services.GetRequiredService<AISelector>());

        // Show LoginView
        var authService = Services.GetRequiredService<IAuthService>();
        var loginView = new LoginView(authService);
        loginView.Show();
    }

    private static void LoadOllamaSettings(AISelector selector)
    {
        try
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MindForge", "settings.json");

            if (!System.IO.File.Exists(path)) return;

            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("ollamaUrl", out var el))
            {
                var url = el.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                    selector.SetOllamaUrl(url);
            }
        }
        catch { /* ignore — defaults are fine */ }
    }

    internal static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath)!);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
            File.AppendAllText(ErrorLogPath, line);
        }
        catch { /* logging must never throw */ }
    }
}
