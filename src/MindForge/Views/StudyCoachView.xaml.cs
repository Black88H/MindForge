using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class StudyCoachView : UserControl
{
    private readonly IAIStudyCoachService _coach;
    private Guid _userId;

    public StudyCoachView()
    {
        InitializeComponent();
        _coach  = App.Services.GetRequiredService<IAIStudyCoachService>();
        _userId = UserSession.UserId;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        TxtLoading.Visibility = Visibility.Visible;
        RecsPanel.Children.Clear();
        RecsPanel.Children.Add(TxtLoading);

        try
        {
            var tipTask    = _coach.GetQuickTipAsync(_userId);
            var reportTask = _coach.GetRecommendationsAsync(_userId);
            await Task.WhenAll(tipTask, reportTask);

            var tip    = await tipTask;
            var report = await reportTask;

            // Summary cards
            TxtBurnout.Text     = report.BurnoutRisk switch { "high" => "🔴 Hoch", "medium" => "🟡 Mittel", _ => "🟢 Niedrig" };
            TxtBurnout.Foreground = new SolidColorBrush(report.BurnoutRisk switch
            {
                "high"   => Colors.OrangeRed,
                "medium" => Colors.Gold,
                _        => Color.FromRgb(34, 197, 94)
            });
            TxtDailyMin.Text     = report.RecommendedDailyMinutes + " min";
            TxtWeeklyGoal.Text   = report.WeeklyGoal;
            TxtEncouragement.Text = report.Encouragement;
            TxtTip.Text          = tip;

            // Recommendations
            RecsPanel.Children.Clear();
            if (report.Recommendations.Count == 0)
            {
                RecsPanel.Children.Add(new TextBlock
                {
                    Text = "Keine Empfehlungen – du machst alles richtig! 🎉",
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0)
                });
                return;
            }

            foreach (var rec in report.Recommendations)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 32, 48)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var stack = new StackPanel();

                // Title row
                var titleRow = new Grid();
                titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var titleBlock = new TextBlock
                {
                    Text = rec.Title,
                    Foreground = new SolidColorBrush(Colors.White),
                    FontSize = 15, FontWeight = FontWeights.SemiBold
                };
                Grid.SetColumn(titleBlock, 0);

                var priorityColor = rec.Priority switch
                {
                    "high"   => Color.FromRgb(239, 68, 68),
                    "medium" => Color.FromRgb(234, 179, 8),
                    _        => Color.FromRgb(34, 197, 94)
                };
                var badge = new Border
                {
                    Background = new SolidColorBrush(priorityColor),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 2, 8, 2),
                    Child = new TextBlock
                    {
                        Text = rec.Priority.ToUpper(),
                        FontSize = 10, FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Colors.White)
                    }
                };
                Grid.SetColumn(badge, 1);
                titleRow.Children.Add(titleBlock);
                titleRow.Children.Add(badge);
                stack.Children.Add(titleRow);

                // Description
                stack.Children.Add(new TextBlock
                {
                    Text = rec.Description,
                    Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                    FontSize = 13, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 8)
                });

                // Action steps
                foreach (var step in rec.ActionSteps)
                    stack.Children.Add(new TextBlock
                    {
                        Text = "→ " + step,
                        Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                        FontSize = 12, Margin = new Thickness(0, 2, 0, 0)
                    });

                border.Child = stack;
                RecsPanel.Children.Add(border);
            }
        }
        catch (Exception ex)
        {
            RecsPanel.Children.Clear();
            RecsPanel.Children.Add(new TextBlock
            {
                Text = "Fehler beim Laden: " + ex.Message,
                Foreground = new SolidColorBrush(Colors.OrangeRed),
                FontSize = 13, TextWrapping = TextWrapping.Wrap
            });
        }
    }

    private async void OnRefresh(object sender, RoutedEventArgs e)
        => await LoadAsync();

    private async void OnNewTip(object sender, RoutedEventArgs e)
    {
        TxtTip.Text = "Lade...";
        try { TxtTip.Text = await _coach.GetQuickTipAsync(_userId); }
        catch { TxtTip.Text = "Tipp konnte nicht geladen werden."; }
    }
}
