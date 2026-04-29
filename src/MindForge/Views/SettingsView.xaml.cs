using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;

namespace MindForge.Views;

public partial class SettingsView : UserControl
{
    private const string CurrentVersion = "v2.0.0"; 

    public SettingsView()
    {
        InitializeComponent();
    }

    private string? _downloadAssetUrl;
    private const string GitHubToken = "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";

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        BtnDownloadUpdate.Visibility = Visibility.Collapsed;
        TxtUpdateStatus.Text = "Prüfe auf Updates bei GitHub...";
        TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.White;
        
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MindForge-AutoUpdater");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
            
            string repoUrl = "https://api.github.com/repos/Black88H/MindForge/releases/latest";
            var response = await client.GetStringAsync(repoUrl);
            var json = JsonDocument.Parse(response);
            var latestVersion = json.RootElement.GetProperty("tag_name").GetString();
            
            if (json.RootElement.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
            {
                _downloadAssetUrl = assets[0].GetProperty("url").GetString();
            }
            
            if (latestVersion != null && latestVersion != CurrentVersion && !string.IsNullOrEmpty(_downloadAssetUrl))
            {
                TxtUpdateStatus.Text = $"Version {latestVersion} gefunden! Bereit zum Download.";
                TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Lime;
                BtnDownloadUpdate.Visibility = Visibility.Visible;
            }
            else
            {
                TxtUpdateStatus.Text = $"Du bist auf dem neuesten Stand ({CurrentVersion})! ✔️";
                TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Lime;
            }
        }
        catch (Exception)
        {
            TxtUpdateStatus.Text = "Update-Server (GitHub) momentan nicht erreichbar.";
            TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
    }

    private async void OnDownloadUpdateClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_downloadAssetUrl)) return;
        
        BtnDownloadUpdate.IsEnabled = false;
        BtnCheckUpdates.IsEnabled = false;
        TxtUpdateStatus.Text = "Lade Update herunter... Bitte warten.";
        TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Orange;

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MindForge-AutoUpdater");
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GitHubToken}");
            client.DefaultRequestHeaders.Add("Accept", "application/octet-stream");

            var response = await client.GetAsync(_downloadAssetUrl);
            response.EnsureSuccessStatusCode();

            string tempZipPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MindForgeUpdate.zip");
            using (var fs = new System.IO.FileStream(tempZipPath, System.IO.FileMode.Create))
            {
                await response.Content.CopyToAsync(fs);
            }

            TxtUpdateStatus.Text = "Update geladen! App wird neugestartet...";
            await Task.Delay(1000);

            string installDir = AppDomain.CurrentDomain.BaseDirectory;
            string batPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MindForgeUpdate.bat");
            
            string batContent = $@"
@echo off
timeout /t 2 /nobreak > NUL
powershell -Command ""Expand-Archive -Path '{tempZipPath}' -DestinationPath '{installDir}' -Force""
start """" ""{System.IO.Path.Combine(installDir, "MindForge.exe")}""
del ""%~f0""
";
            System.IO.File.WriteAllText(batPath, batContent);

            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception)
        {
            TxtUpdateStatus.Text = "Fehler beim Herunterladen.";
            TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Red;
            BtnDownloadUpdate.IsEnabled = true;
            BtnCheckUpdates.IsEnabled = true;
        }
    }
}