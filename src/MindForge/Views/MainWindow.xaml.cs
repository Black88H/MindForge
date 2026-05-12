using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Services;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class MainWindow : Window
{
    private readonly IServiceProvider        _services;
    private readonly IBackgroundTaskService  _bgService;
    private          Button?                 _activeNavButton;

    // System-tray icon (Windows.Forms)
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private bool _minimizeToTray = true;

    // Persisted window-bounds file path
    private static string BoundsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MindForge", "window_bounds.json");

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();
        _services  = services;
        _bgService = services.GetRequiredService<IBackgroundTaskService>();

        // Restore saved bounds before the window shows
        RestoreWindowBounds();

        // F11 fullscreen
        KeyDown += OnWindowKeyDown;

        // Chrome corners when maximised / restored
        StateChanged += OnWindowStateChanged;

        // Persist bounds and handle running-task check on close
        Closing += OnWindowClosing;

        // Bind user info from session
        TxtUsername.Text    = UserSession.Username;
        TxtUserInitial.Text = UserSession.Username.Length > 0
            ? UserSession.Username[0].ToString().ToUpper()
            : "U";
        TxtUserLevel.Text = $"Level {UserSession.Level}";

        int maxXP     = UserSession.Level * 1000;
        int currentXP = UserSession.TotalXP % 1000;
        if (currentXP == 0 && UserSession.TotalXP > 0) currentXP = 1000;
        TxtXP.Text  = $"XP Fortschritt ({currentXP} / 1000)";
        PrgXP.Value = currentXP;

        // System-tray icon setup
        InitTrayIcon();

        // Badge: update whenever the task list changes
        _bgService.Tasks.CollectionChanged += (_, _) => UpdateBadge();
        _bgService.TaskCompleted           += (_, _) => UpdateBadge();
        _bgService.TaskFailed              += (_, _) => UpdateBadge();

        NavigateTo("Dashboard", BtnDashboard);
    }

    // ── Tray icon ─────────────────────────────────────────────────────────────

    private void InitTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text    = "MindForge",
            Visible = false,
            Icon    = LoadTrayIcon()
        };

        // Double-click tray icon → restore window
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        // Context menu: Öffnen / Schließen
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Öffnen",  null, (_, _) => RestoreFromTray());
        menu.Items.Add("Beenden", null, (_, _) =>
        {
            _minimizeToTray = false; // suppress the cancel-check; user chose "quit from tray"
            Dispatcher.Invoke(Close);
        });
        _trayIcon.ContextMenuStrip = menu;
    }

    private static System.Drawing.Icon LoadTrayIcon()
    {
        // Try the .ico files shipped next to the executable
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Assets", "icon.ico"),
            Path.Combine(AppContext.BaseDirectory, "Mindforge-removebg-preview.ico")
        ];
        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try { return new System.Drawing.Icon(path); } catch { /* try next */ }
            }
        }
        return System.Drawing.SystemIcons.Application;
    }

    private void RestoreFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            _trayIcon!.Visible = false;
        });
    }

    // ── Badge (running task count on nav button) ───────────────────────────────

    private void UpdateBadge()
    {
        var running = _bgService.Tasks.Count(t => t.IsRunning);
        if (running > 0)
        {
            TaskBadgeCount.Text     = running.ToString();
            TaskBadge.Visibility    = Visibility.Visible;
        }
        else
        {
            TaskBadge.Visibility    = Visibility.Collapsed;
        }
    }

    // ── Tasks button ──────────────────────────────────────────────────────────

    private void OnTasksClick(object sender, RoutedEventArgs e)
    {
        var win = new BackgroundTasksWindow(_bgService) { Owner = this };
        win.ShowDialog();
    }

    // ── Window drag ───────────────────────────────────────────────────────────

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
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

    // ── StateChanged: rounded corners + minimize-to-tray ─────────────────────

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateChromeForState();

        if (WindowState == WindowState.Minimized && _minimizeToTray)
        {
            Hide();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = true;
                _trayIcon.ShowBalloonTip(
                    2000,
                    "MindForge",
                    "Läuft im Hintergrund. Doppelklick zum Öffnen.",
                    System.Windows.Forms.ToolTipIcon.Info);
            }
        }
    }

    // ── Closing: save bounds + check for running tasks ────────────────────────

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var running = _bgService.Tasks.Count(t => t.IsRunning);
        if (running > 0)
        {
            var result = MessageBox.Show(
                $"Es laufen noch {running} Hintergrundaufgabe(n).\n\n" +
                "Möchten Sie alle Aufgaben abbrechen und MindForge schließen?",
                "MindForge – Aufgaben abbrechen?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            // User confirmed — cancel all running tasks before exit
            foreach (var t in _bgService.Tasks.Where(t => t.IsRunning).ToList())
                _bgService.CancelTask(t.TaskId);
        }

        SaveWindowBounds();

        // Clean up tray icon
        _trayIcon?.Dispose();
        _trayIcon = null;
    }

    // ── F11 fullscreen ────────────────────────────────────────────────────────

    private bool         _isFullscreen;
    private WindowState  _preFullscreenState;

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
            WindowStyle   = WindowStyle.None;
            WindowState   = _preFullscreenState;
            _isFullscreen = false;
        }
        else
        {
            _preFullscreenState = WindowState;
            WindowStyle   = WindowStyle.None;
            WindowState   = WindowState.Maximized;
            _isFullscreen = true;
        }
    }

    // ── Chrome: corner radius when maximised ──────────────────────────────────

    private void UpdateChromeForState()
    {
        if (RootBorder is null) return;
        bool max = WindowState == WindowState.Maximized;
        RootBorder.CornerRadius    = max ? new CornerRadius(0) : new CornerRadius(10);
        RootBorder.BorderThickness = max ? new Thickness(0)    : new Thickness(1);
        BtnMaximize.Content        = max ? "🗗" : "🗖";
    }

    // ── Persist window bounds ─────────────────────────────────────────────────

    private void SaveWindowBounds()
    {
        try
        {
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
            "Analytics" => "📊 Lernanalyse",
            "Search"    => "🔍 Globale Suche",
            "Timer"     => "⏱ Lern-Timer",
            "Quiz"      => "🧠 Adaptiver Quiz",
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
            "Analytics" => new AnalyticsView(),
            "Search"    => new GlobalSearchView(),
            "Timer"     => new StudyTimerView(),
            "Quiz"      => new AdaptiveQuizView(),
            "Settings"  => new SettingsView(),
            _           => new DashboardView()
        };
    }
}
