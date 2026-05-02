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

namespace MindForge;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // Database
        services.AddDbContext<MindForgeDbContext>(options =>
            options.UseSqlite("Data Source=mindforge.db"));

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

        // Ensure database schema is up to date
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MindForgeDbContext>();
            db.Database.EnsureCreated();
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
