using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Services;
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
        
        // Datenbank
        services.AddDbContext<MindForgeDbContext>(options =>
            options.UseSqlite("Data Source=mindforge.db"));
        
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
        
        // Datenbank erstellen, falls nicht vorhanden
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MindForgeDbContext>();
            db.Database.EnsureCreated();
        }

        // Zeige LoginView
        var authService = Services.GetRequiredService<IAuthService>();
        var loginView = new LoginView(authService);
        loginView.Show();
    }
}
