using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;

namespace MindForge.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _services;
    private Button? _activeNavButton;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
        
        // Bind user info
        TxtUsername.Text = UserSession.Username;
        TxtUserInitial.Text = UserSession.Username.Length > 0 
            ? UserSession.Username[0].ToString().ToUpper() 
            : "U";
        TxtUserLevel.Text = $"Level {UserSession.Level}";
        
        // Mock XP logic for the Activity Tracker
        int maxXP = UserSession.Level * 1000;
        int currentXP = UserSession.TotalXP % 1000;
        if (currentXP == 0 && UserSession.TotalXP > 0) currentXP = 1000;
        
        TxtXP.Text = $"XP Fortschritt ({currentXP} / 1000)";
        PrgXP.Value = currentXP;
        
        // Navigate to Dashboard by default
        NavigateTo("Dashboard", BtnDashboard);
    }

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string page)
        {
            NavigateTo(page, btn);
        }
    }

    private void NavigateTo(string page, Button navButton)
    {
        // Update active button style
        if (_activeNavButton != null)
            _activeNavButton.Background = System.Windows.Media.Brushes.Transparent;
        
        _activeNavButton = navButton;
        _activeNavButton.SetResourceReference(BackgroundProperty, "NavActiveBackground");

        TxtPageTitle.Text = page switch
        {
            "Dashboard" => "📊 Dashboard",
            "Subjects" => "📚 Fächer",
            "Materials" => "📄 Materialien",
            "Graph" => "🌐 Wissensgraph",
            "Learning" => "🔄 Lernen (Spaced Repetition)",
            "Tests" => "📝 Tests",
            "Chat" => "🤖 KI-Tutor",
            "Analytics" => "📈 Statistiken",
            "Settings" => "⚙️ Einstellungen",
            _ => page
        };

        // Navigate to the matching page
        MainFrame.Content = page switch
        {
            "Dashboard" => new DashboardView(),
            "Subjects" => new SubjectsView(),
            "Materials" => new MaterialsView(),
            "Graph" => new GraphView(),
            "Learning" => new LearningView(),
            "Tests" => new TestsView(),
            "Chat" => new ChatView(),
            "Analytics" => new AnalyticsView(),
            "Settings" => new SettingsView(),
            "Profile" => new ProfileView(),
            _ => new DashboardView()
        };
    }
}
