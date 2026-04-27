using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MindForge.Services;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Providers;
using MindForge.Services.AI.Selection;
using MindForge.Utils;
using MindForge.ViewModels;
using MindForge.Views;

namespace MindForge;

public partial class App : Application
{
    private IHost? _host;

    /// <summary>Root DI container, exposed for XAML-driven view construction (ViewLocatorConverter).</summary>
    public static IServiceProvider Services { get; private set; } = default!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // ── Database ──────────────────────────────────────────────────
                services.AddDbContext<MindForgeDbContext>(options =>
                    options.UseSqlite($"Data Source={MindForgeDbContext.GetDbPath()}"));

                // ── Repositories ──────────────────────────────────────────────
                services.AddScoped<UserProgressRepository>();
                services.AddScoped<SubjectRepository>();
                services.AddScoped<QuestionRepository>();
                services.AddScoped<TestRepository>();
                services.AddScoped<AnalyticsRepository>();
                services.AddScoped<AchievementRepository>();

                // ── Domain Services ───────────────────────────────────────────
                services.AddScoped<AuthService>();
                services.AddScoped<IGamificationService, GamificationService>();
                services.AddScoped<ISpacedRepetitionService, SpacedRepetitionService>();
                services.AddScoped<ILearningPlanService, LearningPlanService>();
                services.AddScoped<IFileIngestionService, FileIngestionService>();
                services.AddScoped<IKnowledgeGraphService, KnowledgeGraphService>();
                services.AddScoped<IChatService, ChatService>();
                services.AddScoped<ITestService, TestService>();
                services.AddScoped<IAPIKeyService, APIKeyService>();
                services.AddScoped<OCRDocumentService>();
                services.AddScoped<TestRunnerService>();
                services.AddScoped<AnalyticsService>();
                services.AddSingleton<UpdateService>();

                // ── AI Stack ──────────────────────────────────────────────────
                services.AddSingleton<InternetDetector>();
                services.AddSingleton<MindForge.Services.AI.Selection.HardwareDetector>();
                services.AddSingleton<TaskAnalyzer>();
                services.AddScoped<ITokenTracker, TokenTrackerService>();
                services.AddSingleton<ClaudeAIProvider>();
                services.AddSingleton<OpenAIProvider>();
                services.AddSingleton<GeminiProvider>();
                services.AddSingleton<OllamaProvider>();
                services.AddScoped<IAISelector, AISelector>();

                // ── ViewModels (Scoped: ViewLocator creates one DI scope per view nav) ─
                services.AddScoped<MainViewModel>();
                services.AddScoped<DashboardViewModel>();
                services.AddScoped<HomeViewModel>();
                services.AddScoped<QAViewModel>();
                services.AddScoped<ContentGeneratorViewModel>();
                services.AddScoped<TestCreatorViewModel>();
                services.AddScoped<AnalyticsViewModel>();
                services.AddScoped<SettingsViewModel>();
                services.AddScoped<LearningViewModel>();
                services.AddScoped<TestsViewModel>();
                services.AddScoped<KIToolsViewModel>();
                services.AddScoped<SubjectsViewModel>();
                services.AddScoped<ProfileViewModel>();
                services.AddScoped<ChatViewModel>();
                services.AddScoped<MaterialLibraryViewModel>();
                services.AddScoped<KnowledgeGraphViewModel>();
                services.AddScoped<LearningPlanViewModel>();
                services.AddTransient<LoginViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();
        Services = _host.Services;

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("Unbehandelter Fehler", args.Exception);
            MessageBox.Show($"Fehler: {args.Exception.Message}", "MindForge Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MindForgeDbContext>();
            try
            {
                await db.Database.MigrateAsync();
                Logger.Info("Datenbank migriert");
            }
            catch (Exception ex)
            {
                Logger.Error("DB-Migration fehlgeschlagen", ex);
            }
        }

        // Show login window; shutdown if user closes without logging in
        using (var loginScope = _host.Services.CreateScope())
        {
            var loginVm     = loginScope.ServiceProvider.GetRequiredService<LoginViewModel>();
            var loginWindow = new LoginView(loginVm);
            var loggedIn    = loginWindow.ShowDialog();
            if (loggedIn != true)
            {
                Shutdown();
                return;
            }
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        Logger.Info("MindForge v1.0.0 gestartet");
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Logger.Info("MindForge beendet");
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
