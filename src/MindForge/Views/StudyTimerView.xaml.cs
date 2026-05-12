using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class StudyTimerView : UserControl
{
    private readonly IStudyTimerService _timer;
    private int _minutesThisSession;

    public StudyTimerView()
    {
        InitializeComponent();
        _timer = App.Services.GetRequiredService<IStudyTimerService>();

        _timer.Tick            += OnTick;
        _timer.PhaseChanged    += OnPhaseChanged;
        _timer.SessionCompleted += OnSessionCompleted;

        Loaded   += (_, _) => UpdateDisplay();
        Unloaded += (_, _) => { /* keep running */ };
    }

    // ── Controls ──────────────────────────────────────────────────────────────

    private void OnStartPause(object sender, RoutedEventArgs e)
    {
        if (_timer.IsRunning)
        {
            _timer.Pause();
            BtnStartPause.Content = "▶ Fortsetzen";
        }
        else
        {
            _timer.Start();
            BtnStartPause.Content = "⏸ Pause";
        }
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        _timer.Skip();
        BtnStartPause.Content = "▶ Start";
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        _timer.Reset();
        BtnStartPause.Content = "▶ Start";
        _minutesThisSession   = 0;
        UpdateDisplay();
    }

    private void OnSettingChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _timer.WorkDuration       = TimeSpan.FromMinutes(SlWork.Value);
        _timer.ShortBreakDuration = TimeSpan.FromMinutes(SlShort.Value);
        _timer.LongBreakDuration  = TimeSpan.FromMinutes(SlLong.Value);

        TxtWorkVal.Text  = $"{(int)SlWork.Value} Min.";
        TxtShortVal.Text = $"{(int)SlShort.Value} Min.";
        TxtLongVal.Text  = $"{(int)SlLong.Value} Min.";

        if (!_timer.IsRunning) UpdateDisplay();
    }

    private async void OnSaveSession(object sender, RoutedEventArgs e)
    {
        await _timer.SaveSessionAsync(UserSession.UserId);
        MessageBox.Show("✅ Session gespeichert!", "MindForge", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Timer callbacks (from background thread → Dispatcher) ─────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(UpdateDisplay);
    }

    private void OnPhaseChanged(object? sender, TimerPhase phase)
    {
        Dispatcher.InvokeAsync(() =>
        {
            TxtPhase.Text = phase switch
            {
                TimerPhase.Work       => "🎯 Arbeitsphase",
                TimerPhase.ShortBreak => "☕ Kurze Pause",
                TimerPhase.LongBreak  => "🌴 Lange Pause",
                _                     => string.Empty
            };
            UpdateDisplay();
        });
    }

    private void OnSessionCompleted(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _minutesThisSession += (int)_timer.WorkDuration.TotalMinutes;
            TxtMinutes.Text      = _minutesThisSession.ToString();
            UpdateDots();
        });
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void UpdateDisplay()
    {
        var rem = _timer.Remaining;
        TxtTime.Text      = $"{(int)rem.TotalMinutes:D2}:{rem.Seconds:D2}";
        TxtPomodoros.Text = _timer.CompletedPomodoros.ToString();
        UpdateProgressRing();
        UpdateDots();
    }

    private void UpdateProgressRing()
    {
        ProgressCanvas.Children.Clear();

        double total = _timer.Phase switch
        {
            TimerPhase.Work       => _timer.WorkDuration.TotalSeconds,
            TimerPhase.ShortBreak => _timer.ShortBreakDuration.TotalSeconds,
            TimerPhase.LongBreak  => _timer.LongBreakDuration.TotalSeconds,
            _                     => _timer.WorkDuration.TotalSeconds
        };

        double elapsed = total - _timer.Remaining.TotalSeconds;
        double fraction = total > 0 ? elapsed / total : 0;
        fraction = Math.Clamp(fraction, 0, 0.9999);

        const double cx = 130, cy = 130, r = 114, thickness = 16;
        double angle  = fraction * 360;
        double rad    = (angle - 90) * Math.PI / 180;
        double x      = cx + r * Math.Cos(rad);
        double y      = cy + r * Math.Sin(rad);
        bool   large  = angle > 180;

        var path = new System.Windows.Shapes.Path
        {
            Stroke          = (Brush)FindResource("AccentBrush"),
            StrokeThickness = thickness,
            Data            = new PathGeometry
            {
                Figures =
                {
                    new PathFigure
                    {
                        StartPoint = new Point(cx, cy - r),
                        Segments   =
                        {
                            new ArcSegment(
                                new Point(x, y),
                                new Size(r, r), 0, large,
                                SweepDirection.Clockwise, true)
                        }
                    }
                }
            }
        };
        ProgressCanvas.Children.Add(path);
    }

    private void UpdateDots()
    {
        var accentBrush = (Brush)FindResource("AccentBrush");
        var mutedBrush  = (Brush)FindResource("BgTertiaryBrush");
        int done        = _timer.CompletedPomodoros % 4;
        Dot1.Fill = done >= 1 ? accentBrush : mutedBrush;
        Dot2.Fill = done >= 2 ? accentBrush : mutedBrush;
        Dot3.Fill = done >= 3 ? accentBrush : mutedBrush;
        Dot4.Fill = done >= 4 ? accentBrush : mutedBrush;
    }
}
