using System;
using System.IO;
using System.Text.Json;
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

    // Persisted window-bounds file path
    private static string BoundsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MindForge", "window_bounds.json");

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;

        // Restore saved bounds before the window shows
        RestoreWindowBounds();

        // Update F11 key binding at window level
        KeyDown += OnWindowKeyDown;

        // Save bounds on close
        Closing += (_, _) => SaveWindowBounds();

        // Keep CornerRadius=0 when maximised (rounded corners on a maximised window
        // extend off-screen which looks bad)
        StateChanged += (_, _) => UpdateChromeForState();

        // Bind user info from session
        TxtUsername.Text = UserSession.Username;
        TxtUserInitial.Text = UserSession.Username.Length > 0
            ? UserSession.Username[0].ToString().ToUpper()
            : "U";
        TxtUserLevel.Text = $"Level {UserSession.Level}";

        int maxXP     = UserSession.Level * 1000;
        int currentXP = UserSession.TotalXP % 1000;
        if (currentXP == 0 && UserSession.TotalXP > 0) currentXP = 1000;
        TxtXP.Text    = $"XP Fortschritt ({currentXP} / 1000)";
        PrgXP.Value   = currentXP;

        NavigateTo("Dashboard", BtnDashboard);
    }

    // ── Window drag ───────────────────────────────────────────────────────────

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            // Double-click on title bar → maximise / restore
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            DragMove();
        }
    }

    // ── Window controls ───────────────────────────────────────────────────────

    private void OnMinimize(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void OnMaximizeRestore(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void OnClose(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    // ── F11 fullscreen ────────────────────────────────────────────────────────

    private bool _isFullscreen;
    private WindowState _preFullscreenState;

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            // Restore
            WindowStyle  = WindowStyle.None;   // keep frameless look
            WindowState  = _preFullscreenState;
            _isFullscreen = false;
        }
        else
        {
            _preFullscreenState = WindowState;
            WindowStyle  = WindowStyle.None;
            WindowState  = WindowState.Maximized;
            _isFullscreen = true;
        }
    }

    // ── Chrome: corner radius when maximised ──────────────────────────────────

    private void UpdateChromeForState()
    {
        if (RootBorder is null) return;
        bool max = WindowState == WindowState.Maximized;
        RootBorder.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(10);
        RootBorder.BorderThickness = max ? new Thickness(0) : new Thickness(1);
        BtnMaximize.Content = max ? "🗗" : "🗖";
    }

    // ── Persist window bounds ─────────────────────────────────────────────────

    private void SaveWindowBounds()
    {
        try
        {
            // Only save normal-state bounds so we restore to a sensible position
            if (WindowState == WindowState.Normal)
            {
                var bounds = new WindowBoundsDto(Left, Top, Width, Height, false);
                var dir    = Path.GetDirectoryName(BoundsFilePath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(BoundsFilePath,
                    JsonSerializer.Serialize(bounds, new JsonSerializerOptions { WriteIndented = false }));
            }
            else
            {
                // Remember that it was maximised so we can restore that state
                WindowBoundsDto? existing = null;
                if (File.Exists(BoundsFilePath))
                    existing = JsonSerializer.Deserialize<WindowBoundsDto>(File.ReadAllText(BoundsFilePath));

                var bounds = existing is not null
                    ? existing with { Maximized = WindowState == WindowState.Maximized }
                    : new WindowBoundsDto(Left, Top, Width, Height, WindowState == WindowState.Maximized);

                var dir = Path.GetDirectoryName(BoundsFilePath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(BoundsFilePath,
                    JsonSerializer.Serialize(bounds, new JsonSerializerOptions { WriteIndented = false }));
            }
        }
        catch { /* non-critical */ }
    }

    private void RestoreWindowBounds()
    {
        try
        {
            if (!File.Exists(BoundsFilePath)) return;
            var dto = JsonSerializer.Deserialize<WindowBoundsDto>(File.ReadAllText(BoundsFilePath));
            if (dto is null) return;

            // Sanity-check: ensure the bounds are on a visible monitor
            if (dto.Width >= 1000 && dto.Height >= 700)
            {
                Left   = dto.Left;
                Top    = dto.Top;
                Width  = dto.Width;
                Height = dto.Height;
            }

            if (dto.Maximized)
                WindowState = WindowState.Maximized;
        }
        catch { /* non-critical */ }
    }

    private record WindowBoundsDto(double Left, double Top, double Width, double Height, bool Maximized);

    // ── Navigation ────────────────────────────────────────────────────────────

    private void OnNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string page)
            NavigateTo(page, btn);
    }

    private void NavigateTo(string page, Button navButton)
    {
        if (_activeNavButton != null)
            _activeNavButton.Background = System.Windows.Media.Brushes.Transparent;

        _activeNavButton = navButton;
        _activeNavButton.SetResourceReference(BackgroundProperty, "NavActiveBackground");

        TxtPageTitle.Text = page switch
        {
            "Dashboard" => "📊 Dashboard",
            "Subjects"  => "📚 Fächer",
            "Materials" => "📂 Material-Bibliothek",
            "Chat"      => "🤖 KI-Tutor",
            "KITools"   => "🧠 KI-Werkzeuge",
            "Settings"  => "⚙️ Einstellungen",
            _           => page
        };

        MainFrame.Content = page switch
        {
            "Dashboard" => new DashboardView(),
            "Subjects"  => new SubjectsView(),
            "Materials" => new MaterialLibraryView(),
            "Chat"      => new ChatView(),
            "KITools"   => new KIToolsView(),
            "Settings"  => new SettingsView(),
            _           => new DashboardView()
        };
    }
}
