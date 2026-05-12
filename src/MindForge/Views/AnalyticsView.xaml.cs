using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class AnalyticsView : UserControl
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsView()
    {
        InitializeComponent();
        _analytics = App.Services.GetRequiredService<IAnalyticsService>();
        Loaded += async (_, _) => await LoadDataAsync();
    }

    private async System.Threading.Tasks.Task LoadDataAsync()
    {
        try
        {
            var summary = await _analytics.GetSummaryAsync(UserSession.UserId);

            // KPI labels
            KpiMinutes.Text = FormatMinutes(summary.TotalMinutes);
            KpiSessions.Text = summary.TotalSessions.ToString();
            KpiXP.Text       = summary.TotalXP.ToString("N0");
            KpiStreak.Text   = summary.CurrentStreak.ToString();
            KpiTokens.Text   = FormatNumber(summary.TotalTokens);

            // 7-day bar chart
            DrawBarChart(summary.Last7Days);

            // Subject bars
            DrawSubjectBars(summary.BySubject);
        }
        catch
        {
            // Show dashes — DB might be empty
        }
    }

    private void DrawBarChart(System.Collections.Generic.IReadOnlyList<DailyStudyStat> days)
    {
        BarChart.Children.Clear();
        DayLabels.Children.Clear();

        if (days == null || days.Count == 0) return;

        double maxMin = days.Max(d => d.MinutesStudied);
        if (maxMin == 0) maxMin = 1;

        double chartH    = 200;
        double barW      = 28;
        double gap        = 12;
        double totalW    = days.Count * (barW + gap) - gap;
        double startX    = Math.Max(0, (BarChart.ActualWidth - totalW) / 2);
        if (startX <= 0) startX = 10;

        var accentColor = Colors.Indigo;
        if (TryGetAccentColor(out Color c)) accentColor = c;

        for (int i = 0; i < days.Count; i++)
        {
            var day    = days[i];
            double pct = day.MinutesStudied / maxMin;
            double bh  = Math.Max(pct * (chartH - 30), pct > 0 ? 4 : 1);
            double x   = startX + i * (barW + gap);
            double y   = chartH - bh;

            var rect = new Rectangle
            {
                Width           = barW,
                Height          = bh,
                Fill            = pct > 0
                    ? new SolidColorBrush(accentColor)
                    : new SolidColorBrush(Color.FromRgb(40, 44, 56)),
                RadiusX         = 4,
                RadiusY         = 4,
                ToolTip         = $"{day.Date:ddd dd.MM}\n{day.MinutesStudied} Min.\n{day.XPEarned} XP"
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            BarChart.Children.Add(rect);

            // Value label on top of bar
            if (day.MinutesStudied > 0)
            {
                var label = new TextBlock
                {
                    Text       = day.MinutesStudied >= 60
                        ? $"{day.MinutesStudied / 60}h"
                        : $"{day.MinutesStudied}m",
                    FontSize   = 9,
                    Foreground = new SolidColorBrush(Colors.White),
                    Width      = barW,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(label, x);
                Canvas.SetTop(label, y - 14);
                BarChart.Children.Add(label);
            }

            // Day label underneath
            var dayLabel = new TextBlock
            {
                Text       = day.Date.ToString("ddd"),
                FontSize   = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 140, 160)),
                Width      = barW,
                TextAlignment = TextAlignment.Center
            };
            DayLabels.Children.Add(dayLabel);
        }
    }

    private void DrawSubjectBars(System.Collections.Generic.IReadOnlyList<SubjectActivity> subjects)
    {
        SubjectBars.Children.Clear();

        if (subjects == null || subjects.Count == 0)
        {
            TxtNoSubject.Visibility = Visibility.Visible;
            return;
        }
        TxtNoSubject.Visibility = Visibility.Collapsed;

        int maxXP = subjects.Max(s => s.XP);
        if (maxXP == 0) maxXP = 1;

        var accentColor = Colors.Indigo;
        TryGetAccentColor(out accentColor);

        foreach (var s in subjects)
        {
            double pct = (double)s.XP / maxXP;

            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text       = s.SubjectName,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize   = 13
            };
            Grid.SetColumn(name, 0);

            var xpLabel = new TextBlock
            {
                Text       = $"{s.XP} XP",
                Foreground = new SolidColorBrush(accentColor),
                FontSize   = 12
            };
            Grid.SetColumn(xpLabel, 1);

            header.Children.Add(name);
            header.Children.Add(xpLabel);
            row.Children.Add(header);

            var bar = new ProgressBar
            {
                Value           = pct * 100,
                Maximum         = 100,
                Height          = 8,
                Foreground      = new SolidColorBrush(accentColor),
                Background      = new SolidColorBrush(Color.FromRgb(40, 44, 56)),
                BorderThickness = new Thickness(0),
                Margin          = new Thickness(0, 6, 0, 0)
            };
            row.Children.Add(bar);
            SubjectBars.Children.Add(row);
        }
    }

    private bool TryGetAccentColor(out Color color)
    {
        color = Color.FromRgb(99, 102, 241); // indigo fallback
        try
        {
            if (FindResource("AccentBrush") is SolidColorBrush b)
            {
                color = b.Color;
                return true;
            }
        }
        catch { }
        return false;
    }

    private static string FormatMinutes(int total)
    {
        if (total < 60) return total.ToString();
        return $"{total / 60}h{total % 60:D2}m";
    }

    private static string FormatNumber(int n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000.0:F1}M";
        if (n >= 1_000)     return $"{n / 1_000.0:F1}k";
        return n.ToString();
    }
}
