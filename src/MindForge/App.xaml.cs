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

    protected override void OnStartup(StartupEventArgs e)
    {
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
        }

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
}
