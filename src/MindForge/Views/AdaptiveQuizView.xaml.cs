using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Data;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Views;

public partial class AdaptiveQuizView : UserControl
{
    private readonly IAdaptiveQuizService _quiz;
    private readonly MindForgeDbContext   _db;

    private List<QuizQuestion> _questions = [];
    private List<int>          _answers   = [];
    private int                _current;
    private int                _correct;
    private int                _wrong;

    public AdaptiveQuizView()
    {
        InitializeComponent();
        _quiz = App.Services.GetRequiredService<IAdaptiveQuizService>();
        _db   = App.Services.GetRequiredService<MindForgeDbContext>();

        SlCount.ValueChanged += (_, _) => TxtCount.Text = $"{(int)SlCount.Value} Fragen";
        Loaded += LoadNotebooks;
    }

    private async void LoadNotebooks(object sender, RoutedEventArgs e)
    {
        try
        {
            var notebooks = await _db.Notebooks
                .Where(n => n.UserId == UserSession.UserId)
                .OrderBy(n => n.Name)
                .ToListAsync();
            CboNotebook.ItemsSource = notebooks;
            if (notebooks.Any()) CboNotebook.SelectedIndex = 0;
        }
        catch { /* offline */ }
    }

    private async void OnStartQuiz(object sender, RoutedEventArgs e)
    {
        if (CboNotebook.SelectedItem is not Notebook notebook)
        {
            TxtSetupStatus.Text = "Bitte ein Notizbuch auswählen.";
            return;
        }

        var diffMap = new[] { "Easy", "Medium", "Hard" };
        var diff    = diffMap[Math.Max(0, CboDifficulty.SelectedIndex)];
        var count   = (int)SlCount.Value;

        BtnStart.IsEnabled    = false;
        TxtSetupStatus.Text   = "⏳ Fragen werden generiert…";

        try
        {
            _questions = await _quiz.GenerateQuizAsync(notebook.Id, diff, count);
            _answers   = new List<int>(new int[_questions.Count]);
            for (int i = 0; i < _answers.Count; i++) _answers[i] = -1;
            _current   = 0;
            _correct   = 0;
            _wrong     = 0;

            SetupPanel.Visibility  = Visibility.Collapsed;
            QuizPanel.Visibility   = Visibility.Visible;
            ResultPanel.Visibility = Visibility.Collapsed;
            ShowQuestion();
        }
        catch (Exception ex)
        {
            TxtSetupStatus.Text = $"Fehler: {ex.Message}";
        }
        finally
        {
            BtnStart.IsEnabled = true;
        }
    }

    private void ShowQuestion()
    {
        if (_current >= _questions.Count) { ShowResult(); return; }

        var q = _questions[_current];

        PrgQuiz.Value = _questions.Count > 0 ? (double)_current / _questions.Count : 0;
        TxtQNum.Text  = $"Frage {_current + 1} / {_questions.Count}";
        TxtQuestion.Text = q.Question;

        OptionsPanel.Children.Clear();
        ExplanationBorder.Visibility = Visibility.Collapsed;
        BtnNext.Visibility           = Visibility.Collapsed;

        for (int i = 0; i < q.Options.Count; i++)
        {
            var idx = i;
            var btn = new Button
            {
                Content             = $"{(char)('A' + i)}.  {q.Options[i]}",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Height              = 52,
                Margin              = new Thickness(0, 0, 0, 10),
                Padding             = new Thickness(20, 0, 20, 0),
                FontSize            = 14,
                BorderThickness     = new Thickness(1),
                Tag                 = idx,
                Cursor              = System.Windows.Input.Cursors.Hand
            };
            btn.SetResourceReference(BackgroundProperty, "BgTertiaryBrush");
            btn.SetResourceReference(ForegroundProperty, "TextBrush");
            btn.SetResourceReference(BorderBrushProperty, "BorderBrush");
            btn.Click += OnAnswerSelected;
            OptionsPanel.Children.Add(btn);
        }

        // Sidebar
        TxtCorrect.Text  = _correct.ToString();
        TxtWrong.Text    = _wrong.ToString();
        int answered     = _correct + _wrong;
        TxtAccuracy.Text = answered > 0 ? $"{(int)((double)_correct / answered * 100)}%" : "—";
    }

    private void OnAnswerSelected(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        int chosen = (int)btn.Tag;
        var q      = _questions[_current];
        _answers[_current] = chosen;

        bool isCorrect = chosen == q.CorrectIndex;
        if (isCorrect) _correct++; else _wrong++;

        // Colour all buttons
        foreach (Button b in OptionsPanel.Children)
        {
            int idx = (int)b.Tag;
            b.IsEnabled = false;
            if (idx == q.CorrectIndex)
                b.Background = new SolidColorBrush(Color.FromRgb(34, 197, 94));   // green
            else if (idx == chosen && !isCorrect)
                b.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));   // red
            b.Foreground = Brushes.White;
        }

        // Explanation
        ExplanationBorder.Visibility = Visibility.Visible;
        TxtResult.Text       = isCorrect ? "✅ Richtig!" : "❌ Falsch";
        TxtResult.Foreground = isCorrect
            ? new SolidColorBrush(Color.FromRgb(34, 197, 94))
            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
        TxtExplanation.Text  = q.Explanation;
        BtnNext.Visibility   = Visibility.Visible;

        // Sidebar
        TxtCorrect.Text  = _correct.ToString();
        TxtWrong.Text    = _wrong.ToString();
        int answered     = _correct + _wrong;
        TxtAccuracy.Text = answered > 0 ? $"{(int)((double)_correct / answered * 100)}%" : "—";
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        _current++;
        ShowQuestion();
    }

    private void OnAbort(object sender, RoutedEventArgs e)
    {
        ShowResult();
    }

    private async void ShowResult()
    {
        QuizPanel.Visibility   = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Visible;

        var notebook = CboNotebook.SelectedItem as Notebook;
        var result   = await _quiz.EvaluateAnswersAsync(
            UserSession.UserId, notebook?.Id ?? Guid.Empty, _questions, _answers);

        TxtResultEmoji.Text  = result.ScorePercent >= 90 ? "🏆"
                             : result.ScorePercent >= 70 ? "🎉"
                             : result.ScorePercent >= 50 ? "📚" : "💪";
        TxtFinalScore.Text   = $"{(int)result.ScorePercent}%";
        TxtFinalCorrect.Text = $"{result.CorrectAnswers} von {result.TotalQuestions} Fragen richtig";
        TxtFeedback.Text     = result.Feedback;
        TxtXP.Text           = $"+{result.XPEarned} XP";
    }

    private void OnRestartQuiz(object sender, RoutedEventArgs e)
    {
        ResultPanel.Visibility = Visibility.Collapsed;
        SetupPanel.Visibility  = Visibility.Visible;
        TxtSetupStatus.Text    = string.Empty;
    }
}
