using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MindForge.Views;

public class SubjectItem
{
    public Guid   Id       { get; set; }
    public string Name     { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Icon     { get; set; } = string.Empty;
    public double Progress { get; set; }
}

public partial class SubjectsView : UserControl
{
    public ObservableCollection<SubjectItem> Subjects { get; set; }

    // ── Emoji catalogue grouped by topic ────────────────────────────────────
    private static readonly IReadOnlyList<string> EmojiGroups = new[]
    {
        // Study & writing
        "📚","📖","📝","📓","📔","📒","📕","📗","📘","📙","📄","📋","📌","✏️",
        // Science
        "🔬","🧬","🧪","🧫","🔭","⚗️","🌡️","🦠",
        // Maths & data
        "📐","📏","🧮","📊","📈","📉","🔢","💯",
        // Technology
        "💻","🖥️","💾","📱","🎮","⌨️","🖱️","🖨️",
        // Arts & music
        "🎨","🎭","🎵","🎶","🎸","🎹","🎻","🎺",
        // History & geography
        "🌍","🗺️","🏛️","⚔️","📜","🏺","🌐","🏰",
        // Nature & biology
        "🌱","🌿","🌊","🏔️","🦁","🦋","🐬","🌲",
        // Motivation & misc
        "🧠","💡","🔑","🏆","⭐","🌟","🎯","🚀","🔥","⚡","💪","🎓",
    };

    public SubjectsView()
    {
        InitializeComponent();

        Subjects = new ObservableCollection<SubjectItem>
        {
            new() { Id = Guid.NewGuid(), Name = "Informatik",  Subtitle = "Algorithmen & Datenstrukturen", Icon = "💻", Progress = 65 },
            new() { Id = Guid.NewGuid(), Name = "Biologie",    Subtitle = "Zellbiologie & Genetik",        Icon = "🧬", Progress = 30 },
            new() { Id = Guid.NewGuid(), Name = "Mathematik",  Subtitle = "Analysis & Lineare Algebra",    Icon = "📈", Progress = 85 },
        };

        SubjectsList.ItemsSource = Subjects;
        BuildEmojiPalette();
    }

    // ── Build emoji palette buttons at startup ───────────────────────────────
    private void BuildEmojiPalette()
    {
        foreach (var emoji in EmojiGroups)
        {
            var btn = new Button
            {
                Content = emoji,
                Style   = (Style)Resources["EmojiBtn"],
                ToolTip = emoji,
            };
            btn.Click += OnEmojiClick;
            EmojiPalette.Children.Add(btn);
        }
    }

    // ── Emoji selection ──────────────────────────────────────────────────────
    private void OnEmojiClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Content: string emoji }) return;
        SelectEmoji(emoji);
    }

    private void SelectEmoji(string emoji)
    {
        TxtSubjectIcon.Text    = emoji;
        TxtEmojiPreview.Text   = emoji;

        // Briefly highlight the selected button
        foreach (Button btn in EmojiPalette.Children.OfType<Button>())
        {
            btn.Background = btn.Content as string == emoji
                ? (Brush)FindResource("AccentBrush")
                : Brushes.Transparent;
        }
    }

    // ── Dialog open ──────────────────────────────────────────────────────────
    private void OnAddSubjectClick(object sender, RoutedEventArgs e)
    {
        TxtSubjectName.Text     = string.Empty;
        TxtSubjectSubtitle.Text = string.Empty;
        TxtNameError.Visibility = Visibility.Collapsed;
        SelectEmoji("📚");

        ModalOverlay.Visibility = Visibility.Visible;

        // Defer focus until layout is complete
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => TxtSubjectName.Focus());
    }

    // ── Keyboard shortcuts (tunneling PreviewKeyDown) ─────────────────────────
    private void OnModalKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                CloseModal();
                e.Handled = true;
                break;
            case Key.Enter when Keyboard.Modifiers == ModifierKeys.None
                             && sender is not TextBox:
                // Enter on a TextBox is consumed for multi-line support;
                // anywhere else in the dialog it submits the form.
                TrySaveSubject();
                e.Handled = true;
                break;
        }
    }

    // ── Cancel ───────────────────────────────────────────────────────────────
    private void OnCancelModalClick(object sender, RoutedEventArgs e) => CloseModal();

    private void CloseModal() => ModalOverlay.Visibility = Visibility.Collapsed;

    // ── Save ─────────────────────────────────────────────────────────────────
    private void OnSaveSubjectClick(object sender, RoutedEventArgs e) => TrySaveSubject();

    private void TrySaveSubject()
    {
        var name = TxtSubjectName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TxtNameError.Visibility = Visibility.Visible;
            TxtSubjectName.Focus();
            return;
        }

        Subjects.Add(new SubjectItem
        {
            Id       = Guid.NewGuid(),
            Name     = name,
            Subtitle = TxtSubjectSubtitle.Text.Trim(),
            Icon     = string.IsNullOrWhiteSpace(TxtSubjectIcon.Text) ? "📚" : TxtSubjectIcon.Text,
            Progress = 0,
        });

        CloseModal();
    }

    // ── Delete ───────────────────────────────────────────────────────────────
    private void OnDeleteSubjectClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Guid id })
        {
            var subject = Subjects.FirstOrDefault(s => s.Id == id);
            if (subject is not null) Subjects.Remove(subject);
        }
    }
}
