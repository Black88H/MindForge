using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MindForge.Services;
using MindForge.Utils;
using MindForge.ViewModels;

namespace MindForge;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddDbContext<MindForgeDbContext>(options =>
                    options.UseSqlite($"Data Source={MindForgeDbContext.GetDbPath()}"));

                services.AddScoped<UserProgressRepository>();
                services.AddScoped<SubjectRepository>();
                services.AddScoped<QuestionRepository>();
                services.AddScoped<TestRepository>();
                services.AddScoped<AnalyticsRepository>();
                services.AddScoped<AchievementRepository>();

                services.AddTransient<MainViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<QAViewModel>();
                services.AddTransient<ContentGeneratorViewModel>();
                services.AddTransient<TestCreatorViewModel>();
                services.AddTransient<AnalyticsViewModel>();
                services.AddTransient<SettingsViewModel>();

                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

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

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        Logger.Info("MindForge gestartet");
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
