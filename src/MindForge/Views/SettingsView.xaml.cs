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
    // Reads the real assembly version so the display and update check are always
    // accurate after an in-place update (csproj <Version> drives this value).
    private static string CurrentVersion
    {
        get
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "v0.0.0" : $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }
    // Repo is public — no token needed for the API or for downloading releases.
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

            // Public repo: browser_download_url works without auth and is a direct CDN link.
            if (json.RootElement.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                _downloadAssetUrl = assets[0].GetProperty("browser_download_url").GetString();

            if (_latestVersion != null && IsNewerVersion(_latestVersion, CurrentVersion) && !string.IsNullOrEmpty(_downloadAssetUrl))
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

        BtnDownloadUpdate.IsEnabled    = false;
        BtnModalInstall.IsEnabled      = false;
        PrgDownload.Visibility         = Visibility.Visible;
        TxtDownloadProgress.Visibility = Visibility.Visible;
        TxtDownloadProgress.Text       = "Verbinde mit GitHub...";

        try
        {
            // ── Resolve paths ─────────────────────────────────────────────────
            string exePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MindForge.exe");
            string installDir  = Path.GetDirectoryName(exePath)!;
            string tempZipPath = Path.Combine(Path.GetTempPath(), "MindForgeUpdate.zip");

            // ── Download ──────────────────────────────────────────────────────
            // Public repo: browser_download_url is a direct CDN link — no auth needed.
            using var downloadClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            downloadClient.DefaultRequestHeaders.Add("User-Agent", "MindForge-AutoUpdater/3.0");

            TxtDownloadProgress.Text = "Lade Update herunter...";
            using var response = await downloadClient.GetAsync(_downloadAssetUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using (var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var  buffer     = new byte[81920];
                long downloaded = 0;
                int  bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloaded += bytesRead;
                    if (totalBytes > 0)
                    {
                        var pct = (int)((double)downloaded / totalBytes * 100);
                        Dispatcher.Invoke(() =>
                        {
                            PrgDownload.Value        = pct;
                            TxtDownloadProgress.Text = $"{pct}% — {downloaded / 1_048_576.0:F1} MB / {totalBytes / 1_048_576.0:F1} MB";
                        });
                    }
                }
            }

            // Validate — a truncated download would brick the install
            var fi = new FileInfo(tempZipPath);
            if (!fi.Exists || fi.Length < 1024)
                throw new InvalidOperationException($"Download ungültig (Größe: {fi.Length} Bytes). Bitte erneut versuchen.");

            Dispatcher.Invoke(() =>
            {
                PrgDownload.Value        = 100;
                TxtDownloadProgress.Text = "✅ Download fertig. Bereite Installer vor...";
            });

            // ── Build PowerShell updater ──────────────────────────────────────
            // ROOT CAUSE FIX: Expand-Archive -Force on Windows PowerShell 5.1 does NOT
            // overwrite existing files (that capability requires PS 7+). The correct
            // approach is ZipFile::OpenRead + entry.ExtractToFile($dest, $true), which
            // honours the overwrite flag on both PS 5.1 (.NET Framework) and PS 7+.

            string ps1Path = Path.Combine(Path.GetTempPath(), "MindForgeUpdate.ps1");
            string safeZip = tempZipPath.Replace("'", "''");
            string safeDir = installDir.Replace("'", "''");
            string safeExe = exePath.Replace("'", "''");
            string logPath = Path.Combine(Path.GetTempPath(), "MindForgeUpdate_Log.txt").Replace("'", "''");

            // Non-interpolated verbatim string — PS variables stay as-is; only the
            // SAFE_* placeholders are substituted via .Replace() afterwards.
            string ps1Script = @"
$log = 'LOG_PATH'
'[START] MindForge updater running' | Out-File $log -Encoding UTF8 -Force

# ── Wait up to 30 s for old process to exit ──────────────────────────────
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline) {
    if (-not (Get-Process -Name 'MindForge' -ErrorAction SilentlyContinue)) { break }
    Start-Sleep -Milliseconds 500
}
Start-Sleep -Milliseconds 800
'[WAIT] Process exit confirmed' | Out-File $log -Append

# ── Extract ZIP with per-entry overwrite (PS 5.1 compatible) ─────────────
# Expand-Archive -Force does NOT overwrite on PS 5.1; ExtractToFile($path, $true)
# is the correct .NET Framework API for in-place overwriting.
try {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $zip = [System.IO.Compression.ZipFile]::OpenRead('SAFE_ZIP')
    foreach ($entry in $zip.Entries) {
        $dest = Join-Path 'SAFE_DIR' $entry.FullName
        if ($entry.Name -eq '') {
            # Directory entry — ensure it exists
            [System.IO.Directory]::CreateDirectory($dest) | Out-Null
        } else {
            $dir = [System.IO.Path]::GetDirectoryName($dest)
            [System.IO.Directory]::CreateDirectory($dir) | Out-Null
            # ExtractToFile is an extension method — must be called statically on PS 5.1
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $dest, $true)
        }
    }
    $zip.Dispose()
    '[OK] Extraction complete' | Out-File $log -Append
} catch {
    ""[ERROR] $_"" | Out-File $log -Append
    exit 1
}

# ── Verify the new EXE exists before relaunching ─────────────────────────
if (Test-Path -LiteralPath 'SAFE_EXE') {
    Start-Process -FilePath 'SAFE_EXE'
    '[OK] App relaunched' | Out-File $log -Append
} else {
    '[WARN] EXE not found after extraction' | Out-File $log -Append
}

# ── Clean up temp artefacts ───────────────────────────────────────────────
Remove-Item -LiteralPath 'SAFE_ZIP' -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
"
                .Replace("LOG_PATH", logPath)
                .Replace("SAFE_ZIP", safeZip)
                .Replace("SAFE_DIR", safeDir)
                .Replace("SAFE_EXE", safeExe);

            await File.WriteAllTextAsync(ps1Path, ps1Script, System.Text.Encoding.UTF8);

            // ── Confirm with user before closing ─────────────────────────────
            var choice = MessageBox.Show(
                $"Update {_latestVersion} wurde heruntergeladen.\n\n" +
                "MindForge wird jetzt kurz geschlossen, die Dateien werden ersetzt " +
                "und die App startet automatisch neu.\n\n" +
                "Jetzt installieren?",
                "Update installieren",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);

            if (choice != MessageBoxResult.OK)
            {
                // User cancelled — restore buttons
                BtnDownloadUpdate.IsEnabled = true;
                BtnModalInstall.IsEnabled   = true;
                TxtDownloadProgress.Text    = "Installation abgebrochen.";
                return;
            }

            // UseShellExecute = true detaches the updater from this process's Job Object
            // so Windows does NOT kill it when MindForge.exe exits.
            Process.Start(new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-ExecutionPolicy Bypass -NonInteractive -WindowStyle Hidden -File \"{ps1Path}\"",
                UseShellExecute = true,
                WindowStyle     = ProcessWindowStyle.Hidden,
            });

            // Give the PS process a moment to start before we vanish
            await Task.Delay(600);

            // Environment.Exit is more forceful than Shutdown() and guarantees the
            // process handle is released so the updater can overwrite MindForge.exe.
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TxtDownloadProgress.Text       = $"❌ Fehler: {ex.Message}";
                TxtDownloadProgress.Foreground = System.Windows.Media.Brushes.Red;
                BtnDownloadUpdate.IsEnabled    = true;
                BtnModalInstall.IsEnabled      = true;
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

    // ── VERSION COMPARISON ────────────────────────────────────────────────────

    /// <summary>
    /// Returns true only when the GitHub release tag represents a version that is
    /// strictly newer than the currently running assembly version.
    /// Strips leading 'v'/'V' from both strings before comparing.
    /// </summary>
    private static bool IsNewerVersion(string latestTag, string currentVersionStr)
    {
        static Version? TryParse(string s)
        {
            s = s.TrimStart('v', 'V').Trim();
            return Version.TryParse(s, out var v) ? v : null;
        }

        var latest  = TryParse(latestTag);
        var current = TryParse(currentVersionStr);

        return latest != null && current != null && latest > current;
    }

    // ── HELPERS ──────────────────────────────────────────────────────────────

    private static HttpClient CreateGitHubClient()
    {
        // Public repo — no Authorization header needed.
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "MindForge-AutoUpdater/2.1");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        return client;
    }

    private static async Task<JsonDocument> FetchLatestRelease(HttpClient client)
    {
        var json = await client.GetStringAsync(RepoUrl);
        return JsonDocument.Parse(json);
    }
}