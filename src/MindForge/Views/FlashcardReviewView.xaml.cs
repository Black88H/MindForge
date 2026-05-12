using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class FlashcardReviewView : UserControl
{
    private readonly ISpacedRepetitionService _srs;
    private List<SpacedRepetitionItem>        _queue  = [];
    private int                               _index  = 0;
    private int                               _correct = 0;
    private int                               _wrong   = 0;
    private int                               _streak  = 0;
    private readonly Stopwatch                _timer   = new();

    public FlashcardReviewView()
    {
        InitializeComponent();
        _srs = App.Services.GetRequiredService<ISpacedRepetitionService>();
        Loaded += async (_, _) => await LoadQueueAsync();
    }

    private async Task LoadQueueAsync()
    {
        _queue = await _srs.GetOptimalQueueAsync(UserSession.UserId, 30);
        _index = 0; _correct = 0; _wrong = 0; _streak = 0;

        if (_queue.Count == 0)
        {
            EmptyPanel.Visibility  = Visibility.Visible;
            ReviewPanel.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyPanel.Visibility  = Visibility.Collapsed;
        ReviewPanel.Visibility = Visibility.Visible;
        ShowCard();
    }

    private void ShowCard()
    {
        if (_index >= _queue.Count)
        {
            // Session done
            EmptyPanel.Visibility   = Visibility.Visible;
            ReviewPanel.Visibility  = Visibility.Collapsed;
            TxtEmptyMsg.Text        = "Session abgeschlossen! 🎉";
            TxtEmptySub.Text        = $"Richtig: {_correct} | Falsch: {_wrong}";
            return;
        }

        var item = _queue[_index];
        var node = item.KnowledgeNode;

        TxtFront.Text    = node?.Title   ?? "(kein Text)";
        TxtBack.Text     = node?.Summary ?? "(keine Antwort)";
        TxtProgress.Text = $"{_index + 1} / {_queue.Count} Karten";
        TxtStreak.Text   = $"🔥 {_streak}";
        PrgCards.Value   = _queue.Count > 0 ? (double)_index / _queue.Count : 0;

        // Reset card state
        BackBorder.Visibility  = Visibility.Collapsed;
        GradePanel.Visibility  = Visibility.Collapsed;
        BtnFlip.Visibility     = Visibility.Visible;

        // SM-17 stats
        TxtStability.Text   = $"{item.SM17Stability:F1} Tage";
        TxtRetrieval.Text   = $"{item.SM17Retrievability * 100:F0}%";
        TxtDifficulty.Text  = $"{item.SM17Difficulty * 100:F0}%";
        TxtNextReview.Text  = item.NextReviewDate.Date == DateTime.UtcNow.Date
            ? "Heute fällig"
            : item.NextReviewDate.ToString("dd.MM.yyyy");

        TxtSessionCorrect.Text = _correct.ToString();
        TxtSessionWrong.Text   = _wrong.ToString();

        _timer.Restart();
    }

    private void OnFlip(object sender, RoutedEventArgs e)
    {
        BackBorder.Visibility = Visibility.Visible;
        GradePanel.Visibility = Visibility.Visible;
        BtnFlip.Visibility    = Visibility.Collapsed;
        _timer.Stop();
    }

    private async void OnGrade(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || !int.TryParse(btn.Tag?.ToString(), out int grade)) return;

        var item    = _queue[_index];
        var seconds = _timer.Elapsed.TotalSeconds;

        try
        {
            var result = await _srs.ProcessSM17ReviewAsync(item.Id, grade, seconds);
            TxtNextReview.Text = "In " + result.OptimalIntervalDays + " Tagen";
        }
        catch { /* non-critical */ }

        if (grade >= 3) { _correct++; _streak++; }
        else            { _wrong++;   _streak = 0; }

        _index++;
        ShowCard();
    }
}
