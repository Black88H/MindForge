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

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        TxtUpdateStatus.Text = "Prüfe auf Updates bei GitHub...";
        TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.White;
        
        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "MindForge-AutoUpdater");
            
            string repoUrl = "https://api.github.com/repos/Black88H/MindForge/releases/latest";
            var response = await client.GetStringAsync(repoUrl);
            var json = JsonDocument.Parse(response);
            var latestVersion = json.RootElement.GetProperty("tag_name").GetString();
            var browserUrl = json.RootElement.GetProperty("html_url").GetString();
            
            if (latestVersion != null && latestVersion != CurrentVersion)
            {
                TxtUpdateStatus.Text = $"Update auf {latestVersion} verfügbar! Browser wird geöffnet...";
                TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Orange;
                
                await Task.Delay(1500); // Kurz warten damit der User die Nachricht liest
                
                // Öffne GitHub Release Seite
                if (!string.IsNullOrEmpty(browserUrl))
                {
                    Process.Start(new ProcessStartInfo(browserUrl) { UseShellExecute = true });
                }
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
}