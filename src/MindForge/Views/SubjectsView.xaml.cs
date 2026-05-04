using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Style = System.Windows.Style;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MindForge.Data;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.AI;
using UglyToad.PdfPig;

namespace MindForge.Views;

// ── UI data models (in-memory representations, mapped to/from DB) ─────────────

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
    public Guid   Id        { get; set; } = Guid.NewGuid();
    public string Question  { get; set; } = string.Empty;
    public string Answer    { get; set; } = string.Empty;
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

    // ── DB helper — opens a fresh context without going through DI scope ──────
    private static MindForgeDbContext OpenDb()
    {
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MindForge", "mindforge.db");
        return new MindForgeDbContext(
            new DbContextOptionsBuilder<MindForgeDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options);
    }

    public  ObservableCollection<SubjectItem> Subjects { get; set; } = new();
    private SubjectItem?  _activeSubject;
    private NotebookItem? _activeNotebook;
    private CancellationTokenSource? _aiCts;
    private readonly AISelector _ai;
    private bool _settingsPanelVisible = false;
    private int  _flashcardIndex;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SubjectsView()
    {
        InitializeComponent();

        _ai = App.Services.GetRequiredService<AISelector>();

        SubjectsList.ItemsSource = Subjects;
        BuildEmojiPalette();

        Loaded += async (_, _) =>
        {
            await LoadSubjectsFromDbAsync();
            await CheckOllamaAsync();
        };
    }

    // ── DB: Subjects ─────────────────────────────────────────────────────────

    private async Task LoadSubjectsFromDbAsync()
    {
        if (!UserSession.IsAuthenticated) return;
        var userId = UserSession.UserId;

        Subjects.Clear();
        using var db = OpenDb();
        var rows = await db.Subjects
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync();

        foreach (var s in rows)
            Subjects.Add(new SubjectItem
            {
                Id       = s.Id,
                Name     = s.Name,
                Subtitle = s.Description ?? string.Empty,
                Icon     = s.IconKey ?? "📚",
                Progress = 0,
            });
    }

    private async Task SaveSubjectToDbAsync(SubjectItem item)
    {
        if (!UserSession.IsAuthenticated) return;
        using var db = OpenDb();
        db.Subjects.Add(new Subject
        {
            Id          = item.Id,
            UserId      = UserSession.UserId,
            Name        = item.Name,
            Description = item.Subtitle,
            IconKey     = item.Icon,
            Color       = "#6366F1",
            CreatedAt   = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task DeleteSubjectFromDbAsync(Guid id)
    {
        if (!UserSession.IsAuthenticated) return;
        using var db = OpenDb();
        var s = await db.Subjects.FindAsync(id);
        if (s is null) return;
        // Cascade: remove notebooks + their materials + their chat messages
        var nbIds = await db.Notebooks.Where(n => n.SubjectId == id).Select(n => n.Id).ToListAsync();
        foreach (var nbId in nbIds)
        {
            db.Materials.RemoveRange(db.Materials.Where(m => m.NotebookId == nbId));
            db.ChatMessages.RemoveRange(db.ChatMessages.Where(c => c.NotebookId == nbId));
        }
        db.Notebooks.RemoveRange(db.Notebooks.Where(n => n.SubjectId == id));
        db.Materials.RemoveRange(db.Materials.Where(m => m.SubjectId == id && m.NotebookId == null));
        db.Subjects.Remove(s);
        await db.SaveChangesAsync();
    }

    // ── DB: Notebooks ─────────────────────────────────────────────────────────

    private async Task LoadNotebooksFromDbAsync(SubjectItem subject)
    {
        subject.Notebooks.Clear();
        if (!UserSession.IsAuthenticated) return;

        using var db = OpenDb();
        var rows = await db.Notebooks
            .Where(n => n.SubjectId == subject.Id)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync();

        foreach (var n in rows)
        {
            var nb = new NotebookItem
            {
                Id           = n.Id,
                Name         = n.Name,
                LastModified = n.LastModified.ToLocalTime(),
                Progress     = n.Progress,
                ChatCount    = n.ChatCount,
                Settings     = new NotebookSettings
                {
                    LearningLevel    = n.LearningLevel,
                    ExplanationStyle = n.ExplanationStyle,
                }
            };

            // Materials
            var mats = await db.Materials
                .Where(m => m.NotebookId == n.Id)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
            foreach (var m in mats)
                nb.Materials.Add(new NotebookMaterialItem
                {
                    Id      = m.Id,
                    Name    = m.OriginalFileName,
                    Content = m.KiContent,
                    Icon    = m.OriginalFormat == MaterialFormat.PDF  ? "📄" :
                              m.OriginalFormat == MaterialFormat.DOCX ? "📝" :
                              m.OriginalFormat == MaterialFormat.Image ? "🖼️" : "📄",
                });

            // Chat history
            var chats = await db.ChatMessages
                .Where(c => c.NotebookId == n.Id)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
            foreach (var c in chats)
                nb.ChatHistory.Add(new NotebookChatMsg
                {
                    IsUser  = c.Role == ChatRole.User,
                    Content = c.Content,
                    Time    = c.CreatedAt.ToLocalTime().ToString("HH:mm"),
                });

            subject.Notebooks.Add(nb);
        }

        if (subject.Notebooks.Count > 0)
            subject.Progress = subject.Notebooks.Average(n => n.Progress);
    }

    private async Task<Guid> SaveNotebookToDbAsync(Guid subjectId, string name)
    {
        var id  = Guid.NewGuid();
        var now = DateTime.UtcNow;
        if (UserSession.IsAuthenticated)
        {
            using var db = OpenDb();
            db.Notebooks.Add(new Notebook
            {
                Id               = id,
                SubjectId        = subjectId,
                UserId           = UserSession.UserId,
                Name             = name,
                LearningLevel    = "Fortgeschritten",
                ExplanationStyle = "Normal",
                CreatedAt        = now,
                LastModified     = now,
            });
            await db.SaveChangesAsync();
        }
        return id;
    }

    private async Task DeleteNotebookFromDbAsync(Guid id)
    {
        if (!UserSession.IsAuthenticated) return;
        using var db = OpenDb();
        db.Materials.RemoveRange(db.Materials.Where(m => m.NotebookId == id));
        db.ChatMessages.RemoveRange(db.ChatMessages.Where(c => c.NotebookId == id));
        var nb = await db.Notebooks.FindAsync(id);
        if (nb is not null) db.Notebooks.Remove(nb);
        await db.SaveChangesAsync();
    }

    private async Task SaveNotebookProgressAsync()
    {
        if (!UserSession.IsAuthenticated || _activeNotebook is null) return;
        using var db = OpenDb();
        var nb = await db.Notebooks.FindAsync(_activeNotebook.Id);
        if (nb is null) return;
        nb.Progress         = _activeNotebook.Progress;
        nb.ChatCount        = _activeNotebook.ChatCount;
        nb.LastModified     = DateTime.UtcNow;
        nb.LearningLevel    = _activeNotebook.Settings.LearningLevel;
        nb.ExplanationStyle = _activeNotebook.Settings.ExplanationStyle;
        _activeNotebook.LastModified = nb.LastModified.ToLocalTime();
        await db.SaveChangesAsync();
    }

    // ── DB: Materials ─────────────────────────────────────────────────────────

    private async Task<Guid> SaveMaterialToDbAsync(string name, string content, MaterialFormat fmt, string filePath = "")
    {
        var id = Guid.NewGuid();
        if (UserSession.IsAuthenticated && _activeNotebook is not null && _activeSubject is not null)
        {
            using var db = OpenDb();
            db.Materials.Add(new Material
            {
                Id               = id,
                UserId           = UserSession.UserId,
                SubjectId        = _activeSubject.Id,
                NotebookId       = _activeNotebook.Id,
                OriginalFileName = name,
                OriginalFormat   = fmt,
                OriginalFilePath = filePath,
                KiContent        = content,
                TokenCount       = content.Length / 4,
                CreatedAt        = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        return id;
    }

    private async Task DeleteMaterialFromDbAsync(Guid id)
    {
        if (!UserSession.IsAuthenticated) return;
        using var db = OpenDb();
        var m = await db.Materials.FindAsync(id);
        if (m is not null) { db.Materials.Remove(m); await db.SaveChangesAsync(); }
    }

    // ── DB: Chat messages ─────────────────────────────────────────────────────

    private async Task SaveChatMessageAsync(string content, ChatRole role)
    {
        if (!UserSession.IsAuthenticated || _activeNotebook is null || _activeSubject is null) return;
        using var db = OpenDb();
        db.ChatMessages.Add(new ChatMessage
        {
            Id         = Guid.NewGuid(),
            UserId     = UserSession.UserId,
            SubjectId  = _activeSubject.Id,
            NotebookId = _activeNotebook.Id,
            Role       = role,
            Content    = content,
            Provider   = "Ollama",
            CreatedAt  = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    // ── Emoji palette ─────────────────────────────────────────────────────────

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
                _ = TrySaveSubjectAsync(); e.Handled = true; break;
        }
    }

    private void OnCancelModalClick(object sender, RoutedEventArgs e) => CloseModal();
    private void CloseModal() => ModalOverlay.Visibility = Visibility.Collapsed;
    private async void OnSaveSubjectClick(object sender, RoutedEventArgs e) => await TrySaveSubjectAsync();

    private async Task TrySaveSubjectAsync()
    {
        var name = TxtSubjectName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TxtNameError.Visibility = Visibility.Visible;
            TxtSubjectName.Focus();
            return;
        }
        var item = new SubjectItem
        {
            Id       = Guid.NewGuid(),
            Name     = name,
            Subtitle = TxtSubjectSubtitle.Text.Trim(),
            Icon     = string.IsNullOrWhiteSpace(TxtSubjectIcon.Text) ? "📚" : TxtSubjectIcon.Text,
            Progress = 0,
        };
        await SaveSubjectToDbAsync(item);
        Subjects.Add(item);
        CloseModal();
    }

    private async void OnDeleteSubjectClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: Guid id }) return;
        var subject = Subjects.FirstOrDefault(s => s.Id == id);
        if (subject is null) return;
        await DeleteSubjectFromDbAsync(id);
        Subjects.Remove(subject);
    }

    // ── Level 2: Notebooks overlay ────────────────────────────────────────────

    private async void OnSubjectCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SubjectItem subject })
        {
            _activeSubject = subject;
            await LoadNotebooksFromDbAsync(subject);
            OpenNotebooksOverlay(subject);
        }
    }

    private void OpenNotebooksOverlay(SubjectItem subject)
    {
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
        TxtNewNotebookName.Text        = string.Empty;
        TxtNewNotebookError.Visibility = Visibility.Collapsed;
        NewNotebookModal.Visibility    = Visibility.Visible;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,
            () => TxtNewNotebookName.Focus());
    }

    private void OnCancelNewNotebook(object sender, RoutedEventArgs e)
        => NewNotebookModal.Visibility = Visibility.Collapsed;

    private async void OnSaveNewNotebook(object sender, RoutedEventArgs e) => await TrySaveNotebookAsync();

    private async void OnNewNotebookKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { NewNotebookModal.Visibility = Visibility.Collapsed; e.Handled = true; }
        else if (e.Key == Key.Enter) { await TrySaveNotebookAsync(); e.Handled = true; }
    }

    private async Task TrySaveNotebookAsync()
    {
        var name = TxtNewNotebookName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TxtNewNotebookError.Visibility = Visibility.Visible;
            TxtNewNotebookName.Focus();
            return;
        }
        if (_activeSubject is null) return;

        var id = await SaveNotebookToDbAsync(_activeSubject.Id, name);
        _activeSubject.Notebooks.Add(new NotebookItem
        {
            Id           = id,
            Name         = name,
            LastModified = DateTime.Now,
        });
        NewNotebookModal.Visibility = Visibility.Collapsed;
    }

    private async void OnDeleteNotebookClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: Guid id } || _activeSubject is null) return;
        var nb = _activeSubject.Notebooks.FirstOrDefault(n => n.Id == id);
        if (nb is null) return;
        await DeleteNotebookFromDbAsync(id);
        _activeSubject.Notebooks.Remove(nb);
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

        CboLearningLevel.SelectedIndex = notebook.Settings.LearningLevel switch
        {
            "Anfänger" => 0, "Experte" => 2, _ => 1
        };
        CboExplanationStyle.SelectedIndex = notebook.Settings.ExplanationStyle switch
        {
            "Wie für 5-Jährige" => 1, "Technisch/Präzise" => 2, "Mit Beispielen" => 3, _ => 0
        };

        ToolResultPanel.Visibility  = Visibility.Collapsed;
        FlashcardsPanel.Visibility  = Visibility.Collapsed;
        TxtToolStatus.Visibility    = Visibility.Collapsed;
        StreamingBubble.Visibility  = Visibility.Collapsed;
        BtnSendChat.Visibility      = Visibility.Visible;
        BtnStopChat.Visibility      = Visibility.Collapsed;

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

    // ── Materials — paste text ────────────────────────────────────────────────

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

    private async void OnSavePasteText(object sender, RoutedEventArgs e)
    {
        var name    = TxtPasteName.Text.Trim();
        var content = TxtPasteContent.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(content)) return;

        var id = await SaveMaterialToDbAsync(name, content, MaterialFormat.Text);
        _activeNotebook?.Materials.Add(new NotebookMaterialItem { Id = id, Name = name, Content = content });
        PasteTextModal.Visibility = Visibility.Collapsed;
        RefreshDetailProgress();
        await SaveNotebookProgressAsync();
    }

    private void OnPasteTextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) PasteTextModal.Visibility = Visibility.Collapsed;
    }

    // ── Materials — file upload ───────────────────────────────────────────────

    private async void OnUploadFileClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Datei hochladen",
            Filter = "Unterstützte Dateien|*.pdf;*.docx;*.txt;*.md;*.cs;*.py;*.js;*.ts;*.java;*.cpp;*.html;*.css;*.csv;*.json;*.xml;*.rtf|" +
                     "PDF|*.pdf|Word-Dokument|*.docx|Text / Code|*.txt;*.md;*.cs;*.py;*.js;*.ts;*.java;*.cpp;*.html;*.css|" +
                     "Daten|*.csv;*.json;*.xml|Alle Dateien|*.*",
            Multiselect = false,
        };
        if (dlg.ShowDialog() != true) return;

        var filePath = dlg.FileName;
        var fileName = Path.GetFileName(filePath);
        var ext      = Path.GetExtension(filePath).ToLowerInvariant();

        // Show progress in paste modal temporarily
        TxtPasteName.Text         = Path.GetFileNameWithoutExtension(filePath);
        TxtPasteContent.Text      = "⏳ Datei wird gelesen…";
        PasteTextModal.Visibility = Visibility.Visible;
        await Task.Delay(50); // let UI refresh

        string content;
        MaterialFormat fmt;
        try
        {
            (content, fmt) = ext switch
            {
                ".pdf"  => (await ExtractPdfTextAsync(filePath),                 MaterialFormat.PDF),
                ".docx" => (await Task.Run(() => ExtractDocxText(filePath)),     MaterialFormat.DOCX),
                _       => (await File.ReadAllTextAsync(filePath),               MaterialFormat.Text),
            };
            if (string.IsNullOrWhiteSpace(content)) content = "[Kein lesbarer Text gefunden]";
            if (content.Length > 50_000)            content  = content[..50_000] + "\n[… Inhalt gekürzt …]";
        }
        catch (Exception ex)
        {
            content = $"[Lesefehler: {ex.Message}]";
            fmt     = MaterialFormat.Text;
        }

        PasteTextModal.Visibility = Visibility.Collapsed;

        var id   = await SaveMaterialToDbAsync(fileName, content, fmt, filePath);
        var icon = fmt switch { MaterialFormat.PDF => "📄", MaterialFormat.DOCX => "📝", _ => "📄" };
        _activeNotebook?.Materials.Add(new NotebookMaterialItem { Id = id, Name = fileName, Content = content, Icon = icon });
        RefreshDetailProgress();
        await SaveNotebookProgressAsync();
    }

    private static Task<string> ExtractPdfTextAsync(string path) => Task.Run(() =>
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(path);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    });

    private static string ExtractDocxText(string path)
    {
        var sb = new StringBuilder();
        using var doc  = WordprocessingDocument.Open(path, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;
        foreach (var para in body.Descendants<Paragraph>())
            sb.AppendLine(para.InnerText);
        return sb.ToString();
    }

    // ── Materials — delete ────────────────────────────────────────────────────

    private async void OnDeleteMaterialClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button { Tag: Guid id } || _activeNotebook is null) return;
        var mat = _activeNotebook.Materials.FirstOrDefault(m => m.Id == id);
        if (mat is null) return;
        await DeleteMaterialFromDbAsync(id);
        _activeNotebook.Materials.Remove(mat);
        RefreshDetailProgress();
        await SaveNotebookProgressAsync();
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
            // Persist user message — inside try so DB errors show as chat messages
            await SaveChatMessageAsync(userText, ChatRole.User);

            if (!await _ai.IsOllamaAvailableAsync(ct))
            {
                OllamaStatusBar.Visibility = Visibility.Visible;
                _activeNotebook.ChatHistory.Add(new NotebookChatMsg
                {
                    IsUser  = false,
                    Content = "⚠️ Ollama ist nicht erreichbar.\n" +
                              "Stelle sicher, dass Ollama läuft: ollama serve\n" +
                              "Standard-URL: http://localhost:11434"
                });
                return;
            }

            // Build system prompt with learning context + materials
            var systemPrompt = BuildSystemPrompt();
            var matContext   = BuildMaterialContext();
            if (!string.IsNullOrEmpty(matContext))
                systemPrompt += "\n\n" + matContext;

            TxtStreamingContent.Text   = "";
            StreamingBubble.Visibility = Visibility.Visible;
            ScrollChatToBottom();

            var sb = new StringBuilder();

            // StreamChatAsync uses /api/chat (messages format) with
            // ResponseHeadersRead so tokens stream in real-time.
            await foreach (var chunk in _ai.StreamChatAsync(systemPrompt, userText, AITask.Chat, ct))
            {
                sb.Append(chunk);
                // We are already on the UI thread (WPF SynchronizationContext),
                // so update the live streaming TextBlock directly — no Dispatcher.Invoke needed.
                TxtStreamingContent.Text = sb.ToString();
                ScrollChatToBottom();
            }

            StreamingBubble.Visibility = Visibility.Collapsed;

            if (sb.Length > 0)
            {
                _activeNotebook.ChatHistory.Add(new NotebookChatMsg { IsUser = false, Content = sb.ToString() });
                await SaveChatMessageAsync(sb.ToString(), ChatRole.Assistant);
                await SaveNotebookProgressAsync();
            }
        }
        catch (OperationCanceledException)
        {
            StreamingBubble.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            StreamingBubble.Visibility = Visibility.Collapsed;
            _activeNotebook?.ChatHistory.Add(new NotebookChatMsg
                { IsUser = false, Content = $"⚠️ Fehler: {ex.GetType().Name}: {ex.Message}" });
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

    private void SetChatBusy(bool busy)
    {
        // Always called from the UI thread — no Dispatcher.Invoke needed.
        TxtChatInput.IsEnabled = !busy;
        BtnSendChat.Visibility = busy ? Visibility.Collapsed : Visibility.Visible;
        BtnStopChat.Visibility = busy ? Visibility.Visible   : Visibility.Collapsed;
    }

    private void ScrollChatToBottom() => Dispatcher.BeginInvoke(() =>
        ChatScrollViewer.ScrollToBottom());

    // ── Settings panel ────────────────────────────────────────────────────────

    private void OnToggleSettings(object sender, RoutedEventArgs e)
    {
        _settingsPanelVisible = !_settingsPanelVisible;
        SettingsContent.Visibility = _settingsPanelVisible ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleSettings.Content  = _settingsPanelVisible ? "▲ Einstellungen" : "⚙ Einstellungen";
    }

    private async void OnLearningLevelChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeNotebook is null || CboLearningLevel.SelectedItem is not ComboBoxItem { Content: string lv }) return;
        _activeNotebook.Settings.LearningLevel = lv;
        await SaveNotebookProgressAsync();
    }

    private async void OnExplanationStyleChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeNotebook is null || CboExplanationStyle.SelectedItem is not ComboBoxItem { Content: string st }) return;
        _activeNotebook.Settings.ExplanationStyle = st;
        await SaveNotebookProgressAsync();
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
            : $"Erstelle eine strukturierte Zusammenfassung der folgenden Lernmaterialien auf Deutsch. Verwende Bullet Points. Max. 400 Wörter.\n\n{ctx}";
    }

    private string BuildELI5Prompt()
    {
        var ctx = BuildMaterialContext();
        return string.IsNullOrEmpty(ctx)
            ? $"Erkläre das Thema '{_activeSubject?.Name}' so einfach wie möglich. Verwende Alltagsbeispiele."
            : $"Erkläre die folgenden Inhalte so einfach wie möglich. Keine Fachbegriffe ohne Erklärung.\n\n{ctx}";
    }

    private string BuildStudyGuidePrompt()
    {
        var ctx   = BuildMaterialContext();
        var base_ = "Erstelle einen Lernleitfaden auf Deutsch mit: 1. Kernkonzepte 2. Wichtige Definitionen 3. Typische Prüfungsfragen mit Antworten 4. Zusammenfassung";
        return string.IsNullOrEmpty(ctx) ? $"{base_}\n\nThema: {_activeSubject?.Name}" : $"{base_}\n\n{ctx}";
    }

    private string BuildQuizPrompt()
    {
        var ctx   = BuildMaterialContext();
        var base_ = "Erstelle 5 Multiple-Choice-Fragen auf Deutsch. Format: Frage, dann A) B) C) D), dann 'Richtig: X'";
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
            if (!await _ai.IsOllamaAvailableAsync(ct)) { OllamaStatusBar.Visibility = Visibility.Visible; SetToolBusy(false, string.Empty); return; }
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
            if (!await _ai.IsOllamaAvailableAsync(ct)) { OllamaStatusBar.Visibility = Visibility.Visible; SetToolBusy(false, string.Empty); return; }
            var ctx    = BuildMaterialContext();
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
                await SaveNotebookProgressAsync();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { TxtToolResult.Text = $"⚠️ Fehler: {ex.Message}"; ToolResultPanel.Visibility = Visibility.Visible; }
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
        { _activeNotebook.Flashcards[_flashcardIndex].IsFlipped ^= true; ShowFlashcard(); }
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
        TxtToolStatus.Text         = status;
        TxtToolStatus.Visibility   = busy ? Visibility.Visible : Visibility.Collapsed;
        PanelToolButtons.IsEnabled = !busy;
    });

    // ── Ollama status ─────────────────────────────────────────────────────────

    private async Task CheckOllamaAsync()
    {
        var ok = await _ai.IsOllamaAvailableAsync();
        Dispatcher.Invoke(() => OllamaStatusBar.Visibility = ok ? Visibility.Collapsed : Visibility.Visible);
    }

    private async void OnRetryOllamaClick(object sender, RoutedEventArgs e) => await CheckOllamaAsync();

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
