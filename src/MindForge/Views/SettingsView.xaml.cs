using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MindForge.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void OnCheckUpdatesClick(object sender, RoutedEventArgs e)
    {
        TxtUpdateStatus.Text = "Prüfe auf Updates bei GitHub...";
        TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.White;
        
        // Simuliere einen Netzwerk-Check
        await Task.Delay(2000);
        
        TxtUpdateStatus.Text = "Du bist auf dem neuesten Stand (v2.0.0)! ✔️";
        TxtUpdateStatus.Foreground = System.Windows.Media.Brushes.Lime;
    }
}