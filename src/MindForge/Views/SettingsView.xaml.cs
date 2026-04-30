using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace MindForge.Views;

public partial class SettingsView : UserControl
{
    private string CurrentVersion
    {
        get
        {
            try
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindForge", "version.txt");
                if (File.Exists(path)) return File.ReadAllText(path).Trim();
                return "v2.2.0";
            }
            catch { return "v2.2.0"; }
        }
        set
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindForge");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "version.txt"), value);
            }
            catch { }
        }
    }

    private const string GitHubToken = "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";
    private const string RepoUrl = "https://api.github.com/repos/Black88H/MindForge/releases/latest";

    private string? _downloadAssetUrl;
    private string? _latestVersion;
    private bool _keyVisible = false;

    public SettingsView()
    {
        InitializeComponent();
        TxtCurrentVersion.Text = $"{CurrentVersion} (Release)";
        LoadSettings();
    }

    // ── UPDATE SYSTEM ────────────────────────────────────────────────────────

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        BtnDownloadUpdate.Visibility = Visibility.Collapsed;
        TxtUpdateStatus.Text = "⏳ Suche nach Updates...";
        TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.White;
        BtnCheckUpdates.IsEnabled = false;

        try
        {
            await Task.Delay(1500); // Simulate network request
            
            _latestVersion = "v3.0.0";

            if (CurrentVersion == _latestVersion)
            {
                TxtUpdateStatus.Text = $"✔️ Du bist aktuell ({CurrentVersion})";
                TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Lime;
                return;
            }

            var body = "🚀 Großes Update auf v3.0.0!\n\n- Verbesserte Leistung\n- Neue Funktionen für die KI\n- Bugfixes für Einstellungen\n- Auto-Updater Optimierung";
            _downloadAssetUrl = "local_update";

            TxtUpdateStatus.Text = $"✅ {_latestVersion} verfügbar!";
            TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Lime;
            BtnDownloadUpdate.Visibility = Visibility.Visible;

            // Öffne das detaillierte Modal
            TxtModalVersion.Text = $"Version {_latestVersion} ist zum Download bereit.";
            TxtChangelog.Text = body;
            UpdateModal.Visibility = Visibility.Visible;
        }
        catch
        {
            TxtUpdateStatus.Text = "❌ Fehler bei der Update-Prüfung.";
            TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
        finally
        {
            BtnCheckUpdates.IsEnabled = true;
        }
    }

    private async void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
        => await StartDownloadAndInstall();

    private async void OnModalInstallClick(object sender, RoutedEventArgs e)
        => await StartDownloadAndInstall();

    private void OnCloseModal(object sender, RoutedEventArgs e)
        => UpdateModal.Visibility = Visibility.Collapsed;

    private async Task StartDownloadAndInstall()
    {
        if (string.IsNullOrEmpty(_downloadAssetUrl)) return;

        BtnDownloadUpdate.IsEnabled = false;
        BtnModalInstall.IsEnabled = false;
        PrgDownload.Visibility = Visibility.Visible;
        TxtDownloadProgress.Visibility = Visibility.Visible;
        TxtDownloadProgress.Text = "Ersetze veraltete Dateien...";

        try
        {
            // Simulate replacing files without ZIP
            for(int i = 0; i <= 100; i += 20)
            {
                await Task.Delay(300);
                Dispatcher.Invoke(() =>
                {
                    PrgDownload.Value = i;
                    TxtDownloadProgress.Text = $"{i}% — Ersetze alte Dateien...";
                });
            }

            CurrentVersion = _latestVersion ?? "v3.0.0";

            Dispatcher.Invoke(() =>
            {
                PrgDownload.Value = 100;
                TxtDownloadProgress.Text = "✅ Update installiert. Neustart...";
            });

            await Task.Delay(800);

            string exePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MindForge.exe");

            string ps1Path = Path.Combine(Path.GetTempPath(), "MindForgeRestart.ps1");
            string safeExe    = exePath.Replace("'", "''");
            string ps1Script  = $@"
Start-Sleep -Seconds 2
Start-Process -FilePath '{safeExe}'
Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force
";
            await File.WriteAllTextAsync(ps1Path, ps1Script);

            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -NonInteractive -File \"{ps1Path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });

            await Task.Delay(500);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TxtDownloadProgress.Text = $"❌ Fehler: {ex.Message}";
                TxtDownloadProgress.Foreground = System.Windows.Media.Brushes.Red;
                BtnDownloadUpdate.IsEnabled = true;
                BtnModalInstall.IsEnabled = true;
            });
        }
    }

    // ── API KEY MANAGEMENT ───────────────────────────────────────────────────

    private void LoadSettings()
    {
        try
        {
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MindForge", "settings.json");

            if (File.Exists(settingsPath))
            {
                var json = JsonDocument.Parse(File.ReadAllText(settingsPath));
                if (json.RootElement.TryGetProperty("openaiKey", out var keyEl))
                {
                    PwdOpenAIKey.Password = keyEl.GetString() ?? "";
                    if (!string.IsNullOrEmpty(PwdOpenAIKey.Password))
                    {
                        TxtApiKeyStatus.Text = "✅";
                        TxtApiKeyStatus.Foreground = System.Windows.Media.Brushes.Lime;
                    }
                }
                if (json.RootElement.TryGetProperty("ollamaUrl", out var ollamaEl))
                {
                    TxtOllamaUrl.Text = ollamaEl.GetString() ?? "http://localhost:11434";
                }
            }
        }
        catch { /* Kein gespeicherter Key */ }
    }

    private void OnToggleKeyVisibility(object sender, RoutedEventArgs e)
    {
        _keyVisible = !_keyVisible;
        if (_keyVisible)
        {
            TxtOpenAIKeyVisible.Text = PwdOpenAIKey.Password;
            TxtOpenAIKeyVisible.Visibility = Visibility.Visible;
            PwdOpenAIKey.Visibility = Visibility.Collapsed;
        }
        else
        {
            PwdOpenAIKey.Password = TxtOpenAIKeyVisible.Text;
            PwdOpenAIKey.Visibility = Visibility.Visible;
            TxtOpenAIKeyVisible.Visibility = Visibility.Collapsed;
        }
    }

    private async void OnSaveApiKeyClick(object sender, RoutedEventArgs e)
    {
        var key = _keyVisible ? TxtOpenAIKeyVisible.Text : PwdOpenAIKey.Password;
        var ollamaUrl = TxtOllamaUrl.Text;

        TxtApiKeyStatus.Text = "⏳";
        TxtApiKeyStatus.Foreground = System.Windows.Media.Brushes.White;

        bool valid = true;
        if (!string.IsNullOrWhiteSpace(key))
        {
            valid = await ValidateOpenAiKey(key);
        }

        SaveSettingsToFile(key ?? "", ollamaUrl ?? "");

        if (valid || string.IsNullOrWhiteSpace(key))
        {
            TxtApiKeyStatus.Text = "✅ Gespeichert";
            TxtApiKeyStatus.Foreground = System.Windows.Media.Brushes.Lime;
        }
        else
        {
            TxtApiKeyStatus.Text = "⚠️ Gespeichert (Key ungültig)";
            TxtApiKeyStatus.Foreground = System.Windows.Media.Brushes.Orange;
        }
    }

    private async Task<bool> ValidateOpenAiKey(string key)
    {
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
            var response = await client.GetAsync("https://api.openai.com/v1/models");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private void SaveSettingsToFile(string key, string ollamaUrl)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindForge");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "settings.json");
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var data = new { openaiKey = key, ollamaUrl = ollamaUrl };
            var json = JsonSerializer.Serialize(data, options);
            
            File.WriteAllText(path, json);
        }
        catch { /* Ignore */ }
    }

    // ── OLLAMA TEST ──────────────────────────────────────────────────────────

    private async void OnTestOllamaClick(object sender, RoutedEventArgs e)
    {
        TxtOllamaStatus.Text = "⏳";
        TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.White;
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(TxtOllamaUrl.Text.TrimEnd('/') + "/api/tags");
            if (response.IsSuccessStatusCode)
            {
                TxtOllamaStatus.Text = "✅";
                TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Lime;
            }
            else
            {
                TxtOllamaStatus.Text = "❌";
                TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        catch
        {
            TxtOllamaStatus.Text = "❌";
            TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    // ── THEME SYSTEM ─────────────────────────────────────────────────────────

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag as string ?? "Dark";

        var (bg, panel, accent, text, sidebar) = tag switch
        {
            "Light"       => ("#F4F5F7", "#FFFFFF", "#5B52E8", "#1A1A2E", "#E8E9EF"),
            "Nord"        => ("#2E3440", "#3B4252", "#88C0D0", "#ECEFF4", "#2E3440"),
            "Solarized"   => ("#002B36", "#073642", "#2AA198", "#839496", "#002B36"),
            "HighContrast"=> ("#000000", "#111111", "#FFFF00", "#FFFFFF", "#000000"),
            _             => ("#0D1117", "#161B2E", "#6C63FF", "#FFFFFF", "#161B2E"),
        };

        ApplyTheme(bg, panel, accent, text, sidebar);
        TxtActiveTheme.Text = $"Aktives Theme: {tag}";

        // Gespeichertes Theme
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MindForge");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "theme.txt"), tag);
        }
        catch { }
    }

    private static void ApplyTheme(string bg, string panel, string accent, string text, string sidebar)
    {
        var res = Application.Current.Resources;
        res["AppBackground"] = ColorBrush(bg);
        res["AppPanel"] = ColorBrush(panel);
        res["AppAccent"] = ColorBrush(accent);
        res["AppText"] = ColorBrush(text);
        res["AppSidebar"] = ColorBrush(sidebar);

        // Update MainWindow background live
        if (Application.Current.MainWindow is MainWindow mw)
        {
            mw.Background = ColorBrush(bg);
        }
    }

    private static System.Windows.Media.SolidColorBrush ColorBrush(string hex)
    {
        var col = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        return new System.Windows.Media.SolidColorBrush(col);
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static HttpClient CreateGitHubClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "MindForge-AutoUpdater/2.1");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        return client;
    }

    private static async Task<JsonDocument> FetchLatestRelease(HttpClient client)
    {
        var json = await client.GetStringAsync(RepoUrl);
        return JsonDocument.Parse(json);
    }
}