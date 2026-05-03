using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Services.AI;

namespace MindForge.Views;

// ── Data models ───────────────────────────────────────────────────────────────

public class SubjectItem
{
    public Guid   Id       { get; set; }
    public string Name     { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Icon     { get; set; } = string.Empty;
    public double Progress { get; set; }
    public ObservableCollection<NotebookItem> Notebooks { get; set; } = new();
}

public class NotebookItem
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public string   Name         { get; set; } = string.Empty;
    public DateTime LastModified { get; set; } = DateTime.Now;
    public double   Progress     { get; set; }
    public int      ChatCount    { get; set; }
    public string   LastModifiedLabel => LastModified.Date == DateTime.Today
        ? $"Heute, {LastModified:HH:mm}"
        : LastModified.Date == DateTime.Today.AddDays(-1)
            ? $"Gestern, {LastModified:HH:mm}"
            : LastModified.ToString("dd.MM.yyyy");

    public ObservableCollection<NotebookMaterialItem> Materials   { get; set; } = new();
    public ObservableCollection<NotebookChatMsg>      ChatHistory { get; set; } = new();
    public ObservableCollection<FlashcardItem>        Flashcards  { get; set; } = new();
    public NotebookSettings Settings { get; set; } = new();
}

public class NotebookMaterialItem
{
    public Guid   Id      { get; set; } = Guid.NewGuid();
    public string Name    { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Icon    { get; set; } = "📄";
}

public class NotebookSettings
{
    public string LearningLevel    { get; set; } = "Fortgeschritten";
    public string ExplanationStyle { get; set; } = "Normal";
}

public class NotebookChatMsg
{
    public bool   IsUser  { get; init; }
    public string Content { get; set; } = string.Empty;
    public string Time    { get; init; } = DateTime.Now.ToString("HH:mm");
}

public class FlashcardItem
{
    public Guid   Id       { get; set; } = Guid.NewGuid();
    public string Question { get; set; } = string.Empty;
    public string Answer   { get; set; } = string.Empty;
    public bool   IsFlipped { get; set; }
}

// ── View ──────────────────────────────────────────────────────────────────────

public partial class SubjectsView : UserControl
{
    private static readonly IReadOnlyList<string> EmojiGroups = new[]
    {
        "📚","📖","📝","📓","📔","📒","📕","📗","📘","📙","📄","📋","📌","✏️",
        "🔬","🧬","🧪","🧫","🔭","⚗️","🌡️","🦠",
        "📐","📏","🧮","📊","📈","📉","🔢","💯",
        "💻","🖥️","💾","📱","🎮","⌨️","🖱️","🖨️",
        "🎨","🎭","🎵","🎶","🎸","🎹","🎻","🎺",
        "🌍","🗺️","🏛️","⚔️","📜","🏺","🌐","🏰",
        "🌱","🌿","🌊","🏔️","🦁","🦋","🐬","🌲",
        "🧠","💡","🔑","🏆","⭐","🌟","🎯","🚀","🔥","⚡","💪","🎓",
    };

    public  ObservableCollection<SubjectItem> Subjects { get; set; }
    private SubjectItem?  _activeSubject;
    private NotebookItem? _activeNotebook;
    private CancellationTokenSource? _aiCts;
    private readonly AISelector _ai;
    private bool _settingsPanelVisible = true;
    private int  _flashcardIndex;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SubjectsView()
    {
        InitializeComponent();

        _ai = App.Services.GetRequiredService<AISelector>();

        Subjects = new ObservableCollection<SubjectItem>
        {
            new() { Id = Guid.NewGuid(), Name = "Informatik",  Subtitle = "Algorithmen & Datenstrukturen", Icon = "💻", Progress = 65 },
            new() { Id = Guid.NewGuid(), Name = "Biologie",    Subtitle = "Zellbiologie & Genetik",        Icon = "🧬", Progress = 30 },
            new() { Id = Guid.NewGuid(), Name = "Mathematik",  Subtitle = "Analysis & Lineare Algebra",    Icon = "📈", Progress = 85 },
        };

        SubjectsList.ItemsSource = Subjects;
        BuildEmojiPalette();
        Loaded += async (_, _) => await CheckOllamaAsync();
    }

    // ── Emoji palette (subject dialog) ────────────────────────────────────────

    private void BuildEmojiPalette()
    {
        foreach (var emoji in EmojiGroups)
        {
            var btn = new Button { Content = emoji, Style = (Style)Resources["EmojiBtn"], ToolTip = emoji };
            btn.Click += OnEmojiClick;
            EmojiPalette.Children.Add(btn);
        }
    }

    private void OnEmojiClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string emoji }) SelectEmoji(emoji);
    }

    private void SelectEmoji(string emoji)
    {
        TxtSubjectIcon.Text  = emoji;
        TxtEmojiPreview.Text = emoji;
        foreach (Button btn in EmojiPalette.Children.OfType<Button>())
            btn.Background = btn.Content as string == emoji
                ? (Brush)FindResource("AccentBrush") : Brushes.Transparent;
    }

    // ── Subject dialog ────────────────────────────────────────────────────────

    private void OnAddSubjectClick(object sender, RoutedEventArgs e)
    {
        TxtSubjectName.Text     = string.Empty;
        TxtSubjectSubtitle.Text = string.Empty;
        TxtNameError.Visibility = Visibility.Collapsed;
        SelectEmoji("📚");
        ModalOverlay.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => TxtSubjectName.Focus());
    }

    private void OnModalKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: CloseModal(); e.Handled = true; break;
            case Key.Enter when Keyboard.Modifiers == ModifierKeys.None && sender is not TextBox:
                TrySaveSubject(); e.Handled = true; break;
        }
    }

    private void OnCancelModalClick(object sender, RoutedEventArgs e) => CloseModal();
    private void CloseModal() => ModalOverlay.Visibility = Visibility.Collapsed;
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

    private void OnDeleteSubjectClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true; // stop bubble to parent card-button
        if (sender is Button { Tag: Guid id })
        {
            var subject = Subjects.FirstOrDefault(s => s.Id == id);
            if (subject is not null) Subjects.Remove(subject);
        }
    }

    // ── Level 2: Notebooks overlay ────────────────────────────────────────────

    private void OnSubjectCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SubjectItem subject })
            OpenNotebooksOverlay(subject);
    }

    private void OpenNotebooksOverlay(SubjectItem subject)
    {
        _activeSubject = subject;
        TxtNbSubjectIcon.Text     = subject.Icon;
        TxtNbSubjectName.Text     = subject.Name;
        TxtNbSubjectSubtitle.Text = subject.Subtitle;
        TxtNbSubjectProgress.Text = $"{subject.Progress:0}%";
        NotebooksList.ItemsSource  = subject.Notebooks;

        NotebooksOverlay.Visibility = Visibility.Visible;
        SlideIn(NotebooksPanelTransform, 700);
    }

    private void OnCloseNotebooksClick(object sender, RoutedEventArgs e) => CloseNotebooksOverlay();
    private void OnNotebooksBackdropClick(object sender, MouseButtonEventArgs e) => CloseNotebooksOverlay();

    private void CloseNotebooksOverlay()
    {
        SlideOut(NotebooksPanelTransform, 700,
            () => Dispatcher.Invoke(() => NotebooksOverlay.Visibility = Visibility.Collapsed));
    }

    // ── Level 2: New Notebook modal ───────────────────────────────────────────

    private void OnAddNotebookClick(object sender, RoutedEventArgs e)
    {
        TxtNewNotebookName.Text       = string.Empty;
        TxtNewNotebookError.Visibility = Visibility.Collapsed;
        NewNotebookModal.Visibility   = Visibility.Visible;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => TxtNewNotebookName.Focus());
    }

    private void OnCancelNewNotebook(object sender, RoutedEventArgs e)
        => NewNotebookModal.Visibility = Visibility.Collapsed;

    private void OnSaveNewNotebook(object sender, RoutedEventArgs e) => TrySaveNotebook();

    private void OnNewNotebookKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { NewNotebookModal.Visibility = Visibility.Collapsed; e.Handled = true; }
        else if (e.Key == Key.Enter) { TrySaveNotebook(); e.Handled = true; }
    }

    private void TrySaveNotebook()
    {
        var name = TxtNewNotebookName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TxtNewNotebookError.Visibility = Visibility.Visible;
            TxtNewNotebookName.Focus();
            return;
        }
        _activeSubject?.Notebooks.Add(new NotebookItem
        {
            Id           = Guid.NewGuid(),
            Name         = name,
            LastModified = DateTime.Now,
        });
        NewNotebookModal.Visibility = Visibility.Collapsed;
    }

    private void OnDeleteNotebookClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: Guid id } && _activeSubject is not null)
        {
            var nb = _activeSubject.Notebooks.FirstOrDefault(n => n.Id == id);
            if (nb is not null) _activeSubject.Notebooks.Remove(nb);
        }
    }

    // ── Level 3: Notebook detail ──────────────────────────────────────────────

    private async void OnNotebookCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: NotebookItem notebook })
        {
            _activeNotebook = notebook;
            OpenNotebookDetail(notebook);
            await CheckOllamaAsync();
        }
    }

    private void OpenNotebookDetail(NotebookItem notebook)
    {
        TxtDetailBreadcrumb.Text = $"{_activeSubject?.Name ?? ""} › {notebook.Name}";
        RefreshDetailProgress();

        DetailMaterialsList.ItemsSource = notebook.Materials;
        DetailChatHistory.ItemsSource   = notebook.ChatHistory;

        // Settings dropdowns — map string → index
        CboLearningLevel.SelectedIndex = notebook.Settings.LearningLevel switch
        {
            "Anfänger" => 0, "Experte" => 2, _ => 1
        };
        CboExplanationStyle.SelectedIndex = notebook.Settings.ExplanationStyle switch
        {
            "Wie für 5-Jährige" => 1, "Technisch/Präzise" => 2, "Mit Beispielen" => 3, _ => 0
        };

        // Reset tool panels
        ToolResultPanel.Visibility  = Visibility.Collapsed;
        FlashcardsPanel.Visibility  = Visibility.Collapsed;
        TxtToolStatus.Visibility    = Visibility.Collapsed;
        StreamingBubble.Visibility  = Visibility.Collapsed;
        BtnSendChat.Visibility      = Visibility.Visible;
        BtnStopChat.Visibility      = Visibility.Collapsed;

        // Animate in
        NotebookDetailOverlay.Opacity    = 0;
        NotebookDetailOverlay.Visibility = Visibility.Visible;
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250));
        NotebookDetailOverlay.BeginAnimation(OpacityProperty, fadeIn);
        SlideIn(DetailPanelTransform, 50);
    }

    private void OnDetailBack(object sender, RoutedEventArgs e)
    {
        _aiCts?.Cancel();
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
        fadeOut.Completed += (_, _) =>
            Dispatcher.Invoke(() => NotebookDetailOverlay.Visibility = Visibility.Collapsed);
        NotebookDetailOverlay.BeginAnimation(OpacityProperty, fadeOut);
    }

    private void RefreshDetailProgress()
    {
        if (_activeNotebook is null) return;
        var matScore  = Math.Min(_activeNotebook.Materials.Count * 15, 40);
        var chatScore = Math.Min(_activeNotebook.ChatCount * 5, 40);
        var cardScore = Math.Min(_activeNotebook.Flashcards.Count * 2, 20);
        var pct = matScore + chatScore + cardScore;
        _activeNotebook.Progress  = pct;
        TxtDetailProgress.Text    = $"{pct}%";
        PrgDetailProgress.Value   = pct;

        if (_activeSubject is not null && _activeSubject.Notebooks.Count > 0)
            _activeSubject.Progress = _activeSubject.Notebooks.Average(n => n.Progress);
    }

    // ── Materials ─────────────────────────────────────────────────────────────

    private void OnPasteTextClick(object sender, RoutedEventArgs e)
    {
        TxtPasteName.Text         = string.Empty;
        TxtPasteContent.Text      = string.Empty;
        PasteTextModal.Visibility = Visibility.Visible;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => TxtPasteName.Focus());
    }

    private void OnCancelPasteText(object sender, RoutedEventArgs e)
        => PasteTextModal.Visibility = Visibility.Collapsed;

    private void OnSavePasteText(object sender, RoutedEventArgs e)
    {
        var name    = TxtPasteName.Text.Trim();
        var content = TxtPasteContent.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(content)) return;
        _activeNotebook?.Materials.Add(new NotebookMaterialItem
            { Name = name, Content = content });
        PasteTextModal.Visibility = Visibility.Collapsed;
        RefreshDetailProgress();
    }

    private void OnPasteTextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) PasteTextModal.Visibility = Visibility.Collapsed;
    }

    private void OnDeleteMaterialClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is Button { Tag: Guid id } && _activeNotebook is not null)
        {
            var mat = _activeNotebook.Materials.FirstOrDefault(m => m.Id == id);
            if (mat is not null) { _activeNotebook.Materials.Remove(mat); RefreshDetailProgress(); }
        }
    }

    // ── Chat ──────────────────────────────────────────────────────────────────

    private async void OnSendChatClick(object sender, RoutedEventArgs e) => await SendChatAsync();

    private async void OnChatKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            await SendChatAsync();
        }
    }

    private void OnStopChatClick(object sender, RoutedEventArgs e)
    {
        _aiCts?.Cancel();
        StreamingBubble.Visibility = Visibility.Collapsed;
        SetChatBusy(false);
    }

    private async Task SendChatAsync()
    {
        if (_activeNotebook is null) return;
        var userText = TxtChatInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(userText)) return;
        TxtChatInput.Text = string.Empty;

        _activeNotebook.ChatHistory.Add(new NotebookChatMsg { IsUser = true, Content = userText });
        _activeNotebook.ChatCount++;
        RefreshDetailProgress();
        SetChatBusy(true);
        ScrollChatToBottom();

        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;

        try
        {
            if (!await _ai.IsOllamaAvailableAsync(ct))
            {
                OllamaStatusBar.Visibility = Visibility.Visible;
                SetChatBusy(false);
                return;
            }

            var prompt = $"{BuildSystemPrompt()}\n\n{BuildMaterialContext()}\n\nFrage: {userText}";
            var (provider, model) = await _ai.SelectAsync(AITask.Chat, ct);

            // Show streaming bubble
            TxtStreamingContent.Text   = "";
            StreamingBubble.Visibility = Visibility.Visible;
            ScrollChatToBottom();

            var sb = new StringBuilder();
            await foreach (var chunk in provider.StreamAsync(model, prompt, ct))
            {
                sb.Append(chunk);
                Dispatcher.Invoke(() =>
                {
                    TxtStreamingContent.Text = sb.ToString();
                    ScrollChatToBottom();
                });
            }

            StreamingBubble.Visibility = Visibility.Collapsed;
            if (sb.Length > 0)
                _activeNotebook.ChatHistory.Add(new NotebookChatMsg
                    { IsUser = false, Content = sb.ToString() });
        }
        catch (OperationCanceledException) { StreamingBubble.Visibility = Visibility.Collapsed; }
        catch (Exception ex)
        {
            StreamingBubble.Visibility = Visibility.Collapsed;
            _activeNotebook.ChatHistory.Add(new NotebookChatMsg
                { IsUser = false, Content = $"⚠️ Fehler: {ex.Message}" });
        }
        finally { SetChatBusy(false); }

        ScrollChatToBottom();
    }

    private string BuildMaterialContext()
    {
        if (_activeNotebook?.Materials.Count is null or 0) return string.Empty;
        var sb = new StringBuilder("=== Lernmaterialien ===\n");
        foreach (var mat in _activeNotebook.Materials)
        {
            sb.AppendLine($"\n--- {mat.Name} ---");
            sb.AppendLine(mat.Content.Length > 2500 ? mat.Content[..2500] + "\n[...]" : mat.Content);
        }
        return sb.ToString();
    }

    private string BuildSystemPrompt()
    {
        if (_activeNotebook is null) return "Du bist ein hilfreicher Lernassistent. Antworte auf Deutsch.";
        var level = _activeNotebook.Settings.LearningLevel;
        var style = _activeNotebook.Settings.ExplanationStyle;

        var levelDesc = level switch
        {
            "Anfänger" => "Der Lernende ist Anfänger ohne Vorkenntnisse. Erkläre alles von Grund auf.",
            "Experte"  => "Der Lernende ist Experte. Verwende Fachbegriffe und tiefgehende Erklärungen.",
            _          => "Der Lernende hat mittlere Vorkenntnisse.",
        };
        var styleDesc = style switch
        {
            "Wie für 5-Jährige" => "Erkläre mit einfacher Sprache, Analogien und Alltagsbeispielen.",
            "Technisch/Präzise" => "Sei präzise und technisch korrekt. Keine Vereinfachungen.",
            "Mit Beispielen"    => "Illustriere jedes Konzept mit einem konkreten Beispiel.",
            _                   => "Erkläre klar und strukturiert.",
        };

        return $"""
            Du bist ein KI-Lernassistent für "{_activeSubject?.Name ?? "dieses Fach"}", Notizbuch "{_activeNotebook.Name}".
            {levelDesc}
            {styleDesc}
            Antworte immer auf Deutsch. Sei präzise und hilfreich.
            Falls Lernmaterialien vorhanden sind, beziehe dich darauf.
            """;
    }

    private void SetChatBusy(bool busy) => Dispatcher.Invoke(() =>
    {
        TxtChatInput.IsEnabled  = !busy;
        BtnSendChat.Visibility  = busy ? Visibility.Collapsed : Visibility.Visible;
        BtnStopChat.Visibility  = busy ? Visibility.Visible   : Visibility.Collapsed;
    });

    private void ScrollChatToBottom() => Dispatcher.BeginInvoke(() =>
        ChatScrollViewer.ScrollToBottom());

    // ── Settings panel ────────────────────────────────────────────────────────

    private void OnToggleSettings(object sender, RoutedEventArgs e)
    {
        _settingsPanelVisible = !_settingsPanelVisible;
        SettingsContent.Visibility = _settingsPanelVisible ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleSettings.Content  = _settingsPanelVisible ? "▲ Einstellungen" : "▼ Einstellungen";
    }

    private void OnLearningLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeNotebook is null || CboLearningLevel.SelectedItem is not ComboBoxItem { Content: string lv }) return;
        _activeNotebook.Settings.LearningLevel = lv;
    }

    private void OnExplanationStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeNotebook is null || CboExplanationStyle.SelectedItem is not ComboBoxItem { Content: string st }) return;
        _activeNotebook.Settings.ExplanationStyle = st;
    }

    // ── Learning tools ────────────────────────────────────────────────────────

    private async void OnSummaryClick(object sender, RoutedEventArgs e)
        => await RunToolAsync("📄 Zusammenfassung", BuildSummaryPrompt, AITask.Summarization);

    private async void OnELI5Click(object sender, RoutedEventArgs e)
        => await RunToolAsync("👶 Einfache Erklärung", BuildELI5Prompt, AITask.QnA);

    private async void OnStudyGuideClick(object sender, RoutedEventArgs e)
        => await RunToolAsync("📚 Lernleitfaden", BuildStudyGuidePrompt, AITask.StudyGuide);

    private async void OnQuizClick(object sender, RoutedEventArgs e)
        => await RunToolAsync("❓ Quiz", BuildQuizPrompt, AITask.QnA);

    private async void OnFlashcardsClick(object sender, RoutedEventArgs e)
        => await GenerateFlashcardsAsync();

    private string BuildSummaryPrompt()
    {
        var ctx = BuildMaterialContext();
        return string.IsNullOrEmpty(ctx)
            ? $"Erstelle eine strukturierte Zusammenfassung zum Thema '{_activeSubject?.Name}' auf Deutsch."
            : $"Erstelle eine strukturierte Zusammenfassung der folgenden Lernmaterialien auf Deutsch. Verwende Bullet Points für Kernkonzepte. Max. 400 Wörter.\n\n{ctx}";
    }

    private string BuildELI5Prompt()
    {
        var ctx = BuildMaterialContext();
        return string.IsNullOrEmpty(ctx)
            ? $"Erkläre das Thema '{_activeSubject?.Name}' auf Deutsch so einfach wie möglich. Verwende Alltagsbeispiele."
            : $"Erkläre die folgenden Inhalte auf Deutsch so einfach wie möglich. Keine Fachbegriffe ohne Erklärung. Verwende Analogien.\n\n{ctx}";
    }

    private string BuildStudyGuidePrompt()
    {
        var ctx = BuildMaterialContext();
        var base_ = $"Erstelle einen Lernleitfaden auf Deutsch mit: 1. Kernkonzepte 2. Wichtige Definitionen 3. Typische Prüfungsfragen mit Antworten 4. Zusammenfassung";
        return string.IsNullOrEmpty(ctx) ? $"{base_}\n\nThema: {_activeSubject?.Name}" : $"{base_}\n\n{ctx}";
    }

    private string BuildQuizPrompt()
    {
        var ctx = BuildMaterialContext();
        var base_ = $"Erstelle 5 Multiple-Choice-Fragen auf Deutsch. Format: Frage, dann A) B) C) D), dann 'Richtig: X'";
        return string.IsNullOrEmpty(ctx) ? $"{base_}\n\nThema: {_activeSubject?.Name}" : $"{base_}\n\n{ctx}";
    }

    private async Task RunToolAsync(string title, Func<string> promptFn, AITask task)
    {
        SetToolBusy(true, $"⏳ {title} wird generiert…");
        FlashcardsPanel.Visibility = Visibility.Collapsed;
        ToolResultPanel.Visibility = Visibility.Visible;
        TxtToolTitle.Text          = title;
        TxtToolResult.Text         = string.Empty;

        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;
        try
        {
            if (!await _ai.IsOllamaAvailableAsync(ct))
            {
                OllamaStatusBar.Visibility = Visibility.Visible;
                SetToolBusy(false, string.Empty);
                return;
            }
            var (provider, model) = await _ai.SelectAsync(task, ct);
            TxtToolResult.Text = await provider.GenerateAsync(model, promptFn(), ct);
        }
        catch (OperationCanceledException) { TxtToolResult.Text = "Abgebrochen."; }
        catch (Exception ex) { TxtToolResult.Text = $"⚠️ Fehler: {ex.Message}"; }
        finally { SetToolBusy(false, string.Empty); }
    }

    private async Task GenerateFlashcardsAsync()
    {
        SetToolBusy(true, "⏳ Lernkarten werden generiert…");
        ToolResultPanel.Visibility = Visibility.Collapsed;
        FlashcardsPanel.Visibility = Visibility.Collapsed;

        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;
        try
        {
            if (!await _ai.IsOllamaAvailableAsync(ct))
            {
                OllamaStatusBar.Visibility = Visibility.Visible;
                SetToolBusy(false, string.Empty);
                return;
            }
            var ctx = BuildMaterialContext();
            var prompt = string.IsNullOrEmpty(ctx)
                ? $"Erstelle 8 Lernkarten zum Thema '{_activeSubject?.Name}' auf Deutsch.\nFormat (getrennt durch ---):\nFRAGE: [Frage]\nANTWORT: [Antwort]\n---"
                : $"Erstelle 8 Lernkarten auf Deutsch aus diesen Materialien.\nFormat:\nFRAGE: [Frage]\nANTWORT: [Antwort]\n---\n\n{ctx}";

            var (provider, model) = await _ai.SelectAsync(AITask.StudyGuide, ct);
            var response = await provider.GenerateAsync(model, prompt, ct);

            _activeNotebook?.Flashcards.Clear();
            foreach (var card in ParseFlashcards(response))
                _activeNotebook?.Flashcards.Add(card);

            if (_activeNotebook?.Flashcards.Count > 0)
            {
                _flashcardIndex = 0;
                FlashcardsPanel.Visibility = Visibility.Visible;
                ShowFlashcard();
                RefreshDetailProgress();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            TxtToolResult.Text         = $"⚠️ Fehler: {ex.Message}";
            ToolResultPanel.Visibility = Visibility.Visible;
        }
        finally { SetToolBusy(false, string.Empty); }
    }

    private static List<FlashcardItem> ParseFlashcards(string raw)
    {
        var cards = new List<FlashcardItem>();
        foreach (var block in raw.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string? q = null, a = null;
            foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("FRAGE:",   StringComparison.OrdinalIgnoreCase)) q = line[6..].Trim();
                if (line.StartsWith("ANTWORT:", StringComparison.OrdinalIgnoreCase)) a = line[8..].Trim();
            }
            if (!string.IsNullOrWhiteSpace(q) && !string.IsNullOrWhiteSpace(a))
                cards.Add(new FlashcardItem { Question = q, Answer = a });
        }
        return cards;
    }

    private void ShowFlashcard()
    {
        if (_activeNotebook is null || _activeNotebook.Flashcards.Count == 0) return;
        var card = _activeNotebook.Flashcards[_flashcardIndex];
        TxtFlashcardCounter.Text  = $"{_flashcardIndex + 1} / {_activeNotebook.Flashcards.Count}";
        TxtFlashcardQuestion.Text = card.Question;
        TxtFlashcardAnswer.Text   = card.Answer;
        FlashcardFront.Visibility = card.IsFlipped ? Visibility.Collapsed : Visibility.Visible;
        FlashcardBack.Visibility  = card.IsFlipped ? Visibility.Visible   : Visibility.Collapsed;
    }

    private void OnFlipCard(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook?.Flashcards.Count > 0)
        {
            _activeNotebook.Flashcards[_flashcardIndex].IsFlipped ^= true;
            ShowFlashcard();
        }
    }

    private void OnPrevCard(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook?.Flashcards.Count > 0)
        {
            _activeNotebook.Flashcards[_flashcardIndex].IsFlipped = false;
            _flashcardIndex = (_flashcardIndex - 1 + _activeNotebook.Flashcards.Count) % _activeNotebook.Flashcards.Count;
            ShowFlashcard();
        }
    }

    private void OnNextCard(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook?.Flashcards.Count > 0)
        {
            _activeNotebook.Flashcards[_flashcardIndex].IsFlipped = false;
            _flashcardIndex = (_flashcardIndex + 1) % _activeNotebook.Flashcards.Count;
            ShowFlashcard();
        }
    }

    private void SetToolBusy(bool busy, string status) => Dispatcher.Invoke(() =>
    {
        TxtToolStatus.Text       = status;
        TxtToolStatus.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        PanelToolButtons.IsEnabled = !busy;
    });

    // ── Ollama status ─────────────────────────────────────────────────────────

    private async Task CheckOllamaAsync()
    {
        var ok = await _ai.IsOllamaAvailableAsync();
        Dispatcher.Invoke(() => OllamaStatusBar.Visibility = ok
            ? Visibility.Collapsed : Visibility.Visible);
    }

    private async void OnRetryOllamaClick(object sender, RoutedEventArgs e)
        => await CheckOllamaAsync();

    // ── Animation helpers ─────────────────────────────────────────────────────

    private static void SlideIn(TranslateTransform t, double from, Action? done = null)
    {
        var a = new DoubleAnimation(from, 0, TimeSpan.FromMilliseconds(300))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        if (done != null) a.Completed += (_, _) => done();
        t.BeginAnimation(TranslateTransform.XProperty, a);
    }

    private static void SlideOut(TranslateTransform t, double to, Action? done = null)
    {
        var a = new DoubleAnimation(0, to, TimeSpan.FromMilliseconds(230))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        if (done != null) a.Completed += (_, _) => done();
        t.BeginAnimation(TranslateTransform.XProperty, a);
    }
}
