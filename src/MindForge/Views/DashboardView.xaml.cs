using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class DashboardView : UserControl
{
    private readonly ISpacedRepetitionService _spacedRepetitionService;

    public DashboardView()
    {
        InitializeComponent();
        
        // Hole Services vom DI Container
        _spacedRepetitionService = App.Services.GetRequiredService<ISpacedRepetitionService>();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Setze User Stats
        TxtWelcome.Text = $"Willkommen zurück, {UserSession.Username}!";
        TxtTotalXP.Text = UserSession.TotalXP.ToString("N0");
        TxtStreak.Text = $"{UserSession.CurrentStreak} Tage";
        TxtLevel.Text = $"Level {UserSession.Level}";

        // Hole Anzahl der fälligen Karten asynchron
        try
        {
            var dueItems = await _spacedRepetitionService.GetDueItemsAsync(UserSession.UserId);
            int count = dueItems.Count;
            TxtDueItems.Text = count.ToString();
            TxtRepetitionStatus.Text = count > 0 ? $"{count} Karten fällig" : "Keine Karten fällig";
        }
        catch
        {
            TxtDueItems.Text = "0";
            TxtRepetitionStatus.Text = "Keine Karten fällig";
        }
    }
}