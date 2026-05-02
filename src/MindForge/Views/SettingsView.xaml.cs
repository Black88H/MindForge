using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MindForge.ViewModels;

namespace MindForge.Views;

public partial class SettingsView : UserControl
{
    private const string CurrentVersion = "v3.0.4";
    private const string GitHubToken = "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";
    private const string RepoUrl = "https://api.github.com/repos/Black88H/MindForge/releases/latest";

    private string? _downloadAssetUrl;
    private string? _latestVersion;
    private bool _keyVisible = false;
    private readonly SettingsViewModel _vm;

    public SettingsView()
    {
        InitializeComponent();
        TxtCurrentVersion.Text = $"{CurrentVersion} (Release)";
        LoadSettings();

        _vm = App.Services.GetRequiredService<SettingsViewModel>();
        _vm.LoadSettings();
    }

    // ── OLLAMA MODEL SELECTION ───────────────────────────────────────────────

    private async void OnRefreshModelsClick(object sender, RoutedEventArgs e)
    {
        BtnRefreshModels.IsEnabled = false;
        TxtModelStatus.Text = "⏳ Loading models...";

        _vm.OllamaUrl = TxtOllamaUrl.Text;
        await _vm.RefreshModelsCommand.ExecuteAsync(null);

        TxtModelStatus.Text = _vm.OllamaStatus;

        CboChatModel.ItemsSource = _vm.AvailableModels;
        CboSumModel.ItemsSource = _vm.AvailableModels;

        if (!string.IsNullOrEmpty(_vm.PreferredChatModel))
            CboChatModel.SelectedItem = _vm.PreferredChatModel;
        if (!string.IsNullOrEmpty(_vm.PreferredSummarizationModel))
            CboSumModel.SelectedItem = _vm.PreferredSummarizationModel;

        BtnRefreshModels.IsEnabled = true;
    }

    private void OnSaveModelsClick(object sender, RoutedEventArgs e)
    {
        _vm.OllamaUrl = TxtOllamaUrl.Text;
        _vm.PreferredChatModel = CboChatModel.SelectedItem as string ?? string.Empty;
        _vm.PreferredSummarizationModel = CboSumModel.SelectedItem as string ?? string.Empty;
        _vm.SaveModelSettingsCommand.Execute(null);
        TxtModelStatus.Text = _vm.OllamaStatus;
    }

    // ── UPDATE SYSTEM ────────────────────────────────────────────────────────

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        BtnDownloadUpdate.Visibility = Visibility.Collapsed;
        TxtUpdateStatus.Text = "⏳ Verbinde mit GitHub...";
        TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.White;
        BtnCheckUpdates.IsEnabled = false;

        try
        {
            using var client = CreateGitHubClient();
            var json = await FetchLatestRelease(client);

            _latestVersion = json.RootElement.GetProperty("tag_name").GetString();
            var body = json.RootElement.GetProperty("body").GetString() ?? "Keine Changelogs verfügbar.";

            if (json.RootElement.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                _downloadAssetUrl = assets[0].GetProperty("url").GetString();

            if (_latestVersion != null && _latestVersion != CurrentVersion && !string.IsNullOrEmpty(_downloadAssetUrl))
            {
                TxtUpdateStatus.Text = $"✅ {_latestVersion} verfügbar!";
                TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Lime;
                BtnDownloadUpdate.Visibility = Visibility.Visible;

                // Öffne das detaillierte Modal
                TxtModalVersion.Text = $"Version {_latestVersion} ist zum Download bereit.";
                TxtChangelog.Text = body;
                UpdateModal.Visibility = Visibility.Visible;
            }
            else
            {
                TxtUpdateStatus.Text = $"✔️ Du bist aktuell ({CurrentVersion})";
                TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Lime;
            }
        }
        catch
        {
            TxtUpdateStatus.Text = "❌ GitHub momentan nicht erreichbar.";
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
        TxtDownloadProgress.Text = "Verbinde mit GitHub...";

        try
        {
            // ── Richtiger EXE-Pfad (funktioniert auch bei SingleFile Publish) ──
            string exePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MindForge.exe");
            string installDir = Path.GetDirectoryName(exePath)!;
            string tempZipPath = Path.Combine(Path.GetTempPath(), "MindForgeUpdate.zip");

            // ── Download via separatem HttpClient (kein Header-Konflikt) ──
            using var downloadClient = new HttpClient();
            downloadClient.DefaultRequestHeaders.Add("User-Agent", "MindForge-AutoUpdater/2.2");
            downloadClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
            downloadClient.DefaultRequestHeaders.Add("Accept", "application/octet-stream");
            downloadClient.Timeout = TimeSpan.FromMinutes(10);

            TxtDownloadProgress.Text = "Lade Update herunter...";
            using var response = await downloadClient.GetAsync(_downloadAssetUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using (var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var buffer = new byte[81920];
                long downloaded = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloaded += bytesRead;
                    if (totalBytes > 0)
                    {
                        var pct = (int)((double)downloaded / totalBytes * 100);
                        var dlMb = downloaded / 1024.0 / 1024.0;
                        var totMb = totalBytes / 1024.0 / 1024.0;
                        Dispatcher.Invoke(() =>
                        {
                            PrgDownload.Value = pct;
                            TxtDownloadProgress.Text = $"{pct}% — {dlMb:F1} MB / {totMb:F1} MB";
                        });
                    }
                }
            }

            Dispatcher.Invoke(() =>
            {
                PrgDownload.Value = 100;
                TxtDownloadProgress.Text = "✅ Download fertig. Installiere Update...";
            });

            await Task.Delay(800);

            // ── PowerShell-Skript statt BAT (kein Anführungszeichen-Problem) ──
            string ps1Path = Path.Combine(Path.GetTempPath(), "MindForgeUpdate.ps1");

            // Pfade mit einfachen Anführungszeichen escapen (PS1 LiteralPath)
            string safeZip    = tempZipPath.Replace("'", "''");
            string safeDir    = installDir.Replace("'", "''");
            string safeExe    = exePath.Replace("'", "''");
            string ps1Script  = $@"
Start-Sleep -Seconds 2
Expand-Archive -LiteralPath '{safeZip}' -DestinationPath '{safeDir}' -Force
Start-Process -FilePath '{safeExe}'
Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force
";
            await File.WriteAllTextAsync(ps1Path, ps1Script);

            // Starte PowerShell-Skript im Hintergrund
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
        res["BgBrush"] = ColorBrush(bg);
        res["BgSecondaryBrush"] = ColorBrush(panel);
        res["AccentBrush"] = ColorBrush(accent);
        res["TextBrush"] = ColorBrush(text);
        res["SidebarBrush"] = ColorBrush(sidebar);

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