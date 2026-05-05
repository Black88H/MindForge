using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
using MindForge.Services.AI.Providers;
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
    public string Language         { get; set; } = "Deutsch";
}

public class NotebookChatMsg
{
    public bool   IsUser  { get; init; }
    public string Content { get; set; } = string.Empty;
    public string Time    { get; init; } = DateTime.Now.ToString("HH:mm");
}

public class FlashcardItem
{
    public Guid      Id          { get; set; } = Guid.NewGuid();
    public string    Question    { get; set; } = string.Empty;
    public string    Answer      { get; set; } = string.Empty;
    public bool      IsFlipped   { get; set; }
    // SM-2 SRS data
    public double    Easiness    { get; set; } = 2.5;
    public int       SrsInterval { get; set; } = 0;
    public int       ReviewCount { get; set; } = 0;
    public DateTime? NextReview  { get; set; }
}

public class FormulaDisplayItem
{
    public string LaTeX       { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category    { get; set; } = string.Empty;
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

    public  ObservableCollection<SubjectItem>      Subjects  { get; set; } = new();
    public  ObservableCollection<FormulaDisplayItem> Formulas { get; set; } = new();

    private SubjectItem?  _activeSubject;
    private NotebookItem? _activeNotebook;
    private CancellationTokenSource? _aiCts;
    private readonly AISelector _ai;
    private readonly RAGService _rag;
    private bool _settingsPanelVisible = false;
    private int  _flashcardIndex;

    // RAG indexing cancellation — separate from chat CTS so we can cancel index only
    private CancellationTokenSource? _indexCts;

    // ── Token-efficiency infrastructure ──────────────────────────────────────
    // In-memory cache for expensive background generations (summary, flashcards, etc.)
    // Key = "<taskType>:<promptHash>", Value = AI response text
    private readonly Dictionary<string, string> _responseCache = new();

    // Token usage log — written to AppData\Roaming\MindForge\token_usage.log
    private static readonly string _tokenLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MindForge", "token_usage.log");

    /// <summary>Rough token estimate: 1 token ≈ 4 characters.</summary>
    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);

    /// <summary>
    /// Prune RAG results so the combined context stays within <paramref name="maxTokens"/>.
    /// Results are assumed to be ordered by relevance (best first).
    /// </summary>
    private static string CompressContext(IEnumerable<RelevantChunk> results, int maxTokens = 2000)
    {
        var sb = new StringBuilder();
        int used = 0;
        foreach (var r in results)
        {
            int cost = EstimateTokens(r.Text) + 10; // +10 for the header line
            if (used + cost > maxTokens) break;
            sb.AppendLine($"[{r.MaterialName}] {r.Text}");
            used += cost;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Trim raw material text to a token budget, adding a truncation marker.
    /// Used for non-RAG fallback context and tool prompts.
    /// </summary>
    private static string TruncateToTokens(string text, int maxTokens)
    {
        int maxChars = maxTokens * 4;
        return text.Length <= maxChars ? text : text[..maxChars] + "\n[… gekürzt]";
    }

    /// <summary>
    /// Try to return a cached response; if not cached, call <paramref name="generate"/>,
    /// cache the result, log token usage, and return it.
    /// </summary>
    private async Task<string> GenerateWithCacheAsync(
        string taskType,
        string prompt,
        Func<Task<string>> generate)
    {
        var key = $"{taskType}:{prompt.GetHashCode()}";
        if (_responseCache.TryGetValue(key, out var cached))
        {
            LogTokenUsage(taskType, 0, 0, cached: true);
            return cached;
        }

        var response = await generate();
        _responseCache[key] = response;
        LogTokenUsage(taskType, EstimateTokens(prompt), EstimateTokens(response));
        return response;
    }

    private static void LogTokenUsage(string operation, int input, int output, bool cached = false)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_tokenLogPath)!);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{operation},{input},{output},{input + output},{(cached ? "CACHE_HIT" : "API_CALL")}\n";
            File.AppendAllText(_tokenLogPath, line);
        }
        catch { /* logging is best-effort */ }
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    public SubjectsView()
    {
        InitializeComponent();

        _ai  = App.Services.GetRequiredService<AISelector>();
        _rag = App.Services.GetRequiredService<RAGService>();

        SubjectsList.ItemsSource = Subjects;
        FormulaList.ItemsSource  = Formulas;
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
                    Language         = n.Language,
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

    // ── Background RAG indexing ───────────────────────────────────────────────

    private void IndexNotebookInBackground(NotebookItem notebook)
    {
        _indexCts?.Cancel();
        _indexCts = new CancellationTokenSource();
        var ct = _indexCts.Token;

        _ = Task.Run(async () =>
        {
            foreach (var mat in notebook.Materials.ToList())
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    await _rag.IndexMaterialAsync(mat.Id, notebook.Id, mat.Name, mat.Content, ct);
                }
                catch { /* indexing is best-effort */ }
            }
        }, ct);
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
        nb.Language         = _activeNotebook.Settings.Language;
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
            Formulas.Clear();
            FormulaPanel.Visibility = Visibility.Collapsed;
            OpenNotebookDetail(notebook);
            IndexNotebookInBackground(notebook);
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
        CboLanguage.SelectedIndex = notebook.Settings.Language switch
        {
            "English" => 1, "Français" => 2, "Español" => 3, _ => 0
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

        if (_activeNotebook is not null)
            _ = Task.Run(() => _rag.IndexMaterialAsync(id, _activeNotebook.Id, name, content));
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

        // Show progress in paste modal temporarily
        TxtPasteName.Text         = Path.GetFileNameWithoutExtension(dlg.FileName);
        TxtPasteContent.Text      = "⏳ Datei wird gelesen…";
        PasteTextModal.Visibility = Visibility.Visible;
        await Task.Delay(50); // let UI refresh

        await UploadFileFromPathAsync(dlg.FileName);

        PasteTextModal.Visibility = Visibility.Collapsed;
    }

    private async Task UploadFileFromPathAsync(string filePath)
    {
        if (_activeNotebook is null) return;
        var fileName = Path.GetFileName(filePath);
        var ext      = Path.GetExtension(filePath).ToLowerInvariant();

        string content;
        MaterialFormat fmt;
        try
        {
            (content, fmt) = ext switch
            {
                ".pdf"  => (await ExtractPdfTextAsync(filePath),             MaterialFormat.PDF),
                ".docx" => (await Task.Run(() => ExtractDocxText(filePath)), MaterialFormat.DOCX),
                _       => (await File.ReadAllTextAsync(filePath),           MaterialFormat.Text),
            };
            if (string.IsNullOrWhiteSpace(content)) content = "[Kein lesbarer Text gefunden]";
            if (content.Length > 50_000)            content  = content[..50_000] + "\n[… Inhalt gekürzt …]";
        }
        catch (Exception ex)
        {
            content = $"[Lesefehler: {ex.Message}]";
            fmt     = MaterialFormat.Text;
        }

        var id   = await SaveMaterialToDbAsync(fileName, content, fmt, filePath);
        var icon = fmt switch { MaterialFormat.PDF => "📄", MaterialFormat.DOCX => "📝", _ => "📄" };
        var matItem = new NotebookMaterialItem { Id = id, Name = fileName, Content = content, Icon = icon };
        _activeNotebook.Materials.Add(matItem);
        RefreshDetailProgress();
        await SaveNotebookProgressAsync();

        // Background RAG indexing for the newly added material
        _ = Task.Run(() => _rag.IndexMaterialAsync(id, _activeNotebook.Id, fileName, content));
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

            // Build system prompt — prefer RAG-retrieved chunks over full context
            var systemPrompt = BuildSystemPrompt();
            var ragChunks    = new List<RelevantChunk>();

            if (_activeNotebook!.Materials.Count > 0)
            {
                try
                {
                    ragChunks = await _rag.SearchAsync(userText, _activeNotebook.Id, topK: 8, ct);
                }
                catch { /* RAG is best-effort */ }
            }

            if (ragChunks.Count > 0)
            {
                // Prune to 2000 tokens so the combined prompt stays manageable
                var ragContext = CompressContext(ragChunks, maxTokens: 2000);
                systemPrompt += $"\n\n=== Quellen ===\n{ragContext}";
            }
            else
            {
                // Fallback: token-capped full-context (no RAG results)
                var matContext = BuildMaterialContext();
                if (!string.IsNullOrEmpty(matContext))
                    systemPrompt += "\n\n" + matContext;
            }

            LogTokenUsage("chat_input", EstimateTokens(systemPrompt) + EstimateTokens(userText), 0);

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
                LogTokenUsage("chat_output", 0, EstimateTokens(sb.ToString()));
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

    /// <summary>
    /// Builds the full-context fallback string when RAG returns no results.
    /// Capped at 1500 tokens total (~6000 chars) to prevent prompt bloat.
    /// </summary>
    private string BuildMaterialContext()
    {
        if (_activeNotebook?.Materials.Count is null or 0) return string.Empty;
        const int MaxContextTokens = 1500;
        const int MaxPerMaterial   = 400; // tokens per material

        var sb = new StringBuilder("=== Materialien ===\n");
        foreach (var mat in _activeNotebook.Materials)
        {
            var text = TruncateToTokens(mat.Content, MaxPerMaterial);
            sb.AppendLine($"[{mat.Name}] {text}");
            if (EstimateTokens(sb.ToString()) >= MaxContextTokens) break;
        }
        return sb.ToString();
    }

    /// <summary>
    /// Compact system prompt — all persona information in ~60 tokens.
    /// </summary>
    private string BuildSystemPrompt()
    {
        if (_activeNotebook is null) return "Lernassistent. Antworte auf Deutsch.";
        var lang = _activeNotebook.Settings.Language;
        var langCmd = lang switch
        {
            "English"  => "Answer in English.",
            "Français" => "Réponds en français.",
            "Español"  => "Responde en español.",
            _          => "Antworte auf Deutsch.",
        };
        var level = _activeNotebook.Settings.LearningLevel switch
        {
            "Anfänger" => "Niveau: Anfänger (einfach erklären).",
            "Experte"  => "Niveau: Experte (Fachbegriffe OK).",
            _          => "Niveau: Fortgeschritten.",
        };
        var style = _activeNotebook.Settings.ExplanationStyle switch
        {
            "Wie für 5-Jährige" => "Stil: Analogien & Alltagsbeispiele.",
            "Technisch/Präzise" => "Stil: präzise, keine Vereinfachungen.",
            "Mit Beispielen"    => "Stil: mit konkreten Beispielen.",
            _                   => string.Empty,
        };
        return $"Lernassistent. Fach: {_activeSubject?.Name ?? "–"}. {level} {style} {langCmd} Nur aus Quellen antworten; zitiere [Quelle: X].";
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

    private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_activeNotebook is null || CboLanguage.SelectedItem is not ComboBoxItem { Content: string lang }) return;
        _activeNotebook.Settings.Language = lang;
        await SaveNotebookProgressAsync();
    }

    // ── Clear chat ────────────────────────────────────────────────────────────

    private async void OnClearChatClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null) return;
        var result = MessageBox.Show(
            "Möchtest du den Chat-Verlauf wirklich löschen?",
            "Chat löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        if (UserSession.IsAuthenticated)
        {
            using var db = OpenDb();
            db.ChatMessages.RemoveRange(db.ChatMessages.Where(c => c.NotebookId == _activeNotebook.Id));
            await db.SaveChangesAsync();
        }

        _activeNotebook.ChatHistory.Clear();
        _activeNotebook.ChatCount = 0;
        RefreshDetailProgress();
        await SaveNotebookProgressAsync();
    }

    // ── Export notebook ───────────────────────────────────────────────────────

    private async void OnExportNotebookClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null) return;

        var dlg = new SaveFileDialog
        {
            Title      = "Notizbuch exportieren",
            FileName   = $"{_activeNotebook.Name}.txt",
            Filter     = "Textdatei|*.txt|Alle Dateien|*.*",
            DefaultExt = ".txt",
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine($"=== {_activeNotebook.Name} ===");
        sb.AppendLine($"Exportiert: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Fach: {_activeSubject?.Name ?? ""}");
        sb.AppendLine();

        if (_activeNotebook.Materials.Count > 0)
        {
            sb.AppendLine("──── MATERIALIEN ────────────────────────────────────");
            foreach (var mat in _activeNotebook.Materials)
            {
                sb.AppendLine();
                sb.AppendLine($"[{mat.Icon} {mat.Name}]");
                sb.AppendLine(mat.Content);
            }
            sb.AppendLine();
        }

        if (_activeNotebook.ChatHistory.Count > 0)
        {
            sb.AppendLine("──── CHAT-VERLAUF ───────────────────────────────────");
            foreach (var msg in _activeNotebook.ChatHistory)
            {
                sb.AppendLine();
                sb.AppendLine($"[{(msg.IsUser ? "Du" : "KI")} {msg.Time}]");
                sb.AppendLine(msg.Content);
            }
        }

        try
        {
            await File.WriteAllTextAsync(dlg.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"Notizbuch erfolgreich exportiert:\n{dlg.FileName}",
                "Export erfolgreich", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export fehlgeschlagen: {ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Import from URL ───────────────────────────────────────────────────────

    private async void OnImportUrlClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null) return;

        var url = ShowInputDialog("URL importieren", "Webseiten-URL eingeben:");
        if (string.IsNullOrWhiteSpace(url)) return;

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;

        SetToolBusy(true, "⏳ Webseite wird geladen…");
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(30);
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) MindForge/3.7.0");

            var html = await http.GetStringAsync(url);

            // Strip HTML tags and entities
            var text = Regex.Replace(html, "<[^>]+>", " ");
            text = Regex.Replace(text, @"&\w+;", " ");
            text = Regex.Replace(text, @"\s{2,}", " ").Trim();
            if (text.Length > 50_000) text = text[..50_000] + "\n[… Inhalt gekürzt …]";

            var uri      = new Uri(url);
            var siteName = uri.Host.Replace("www.", "");
            var name     = $"{siteName} – {DateTime.Now:dd.MM. HH:mm}";

            var id = await SaveMaterialToDbAsync(name, text, MaterialFormat.Text, url);
            _activeNotebook.Materials.Add(new NotebookMaterialItem
                { Id = id, Name = name, Content = text, Icon = "🌐" });

            RefreshDetailProgress();
            await SaveNotebookProgressAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"URL konnte nicht geladen werden:\n{ex.Message}",
                "Import-Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetToolBusy(false, string.Empty);
        }
    }

    private static string? ShowInputDialog(string title, string prompt)
    {
        var dlg = new Window
        {
            Title                 = title,
            Width                 = 480,
            Height                = 180,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle           = WindowStyle.ToolWindow,
            ResizeMode            = ResizeMode.NoResize,
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });
        var textBox = new TextBox
        {
            Height                   = 36,
            Padding                  = new Thickness(8, 0, 8, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize                 = 14,
        };
        panel.Children.Add(textBox);
        var buttons = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin              = new Thickness(0, 12, 0, 0),
        };
        string? result = null;
        var btnOk = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        btnOk.Click += (_, _) => { result = textBox.Text.Trim(); dlg.DialogResult = true; };
        var btnCancel = new Button { Content = "Abbrechen", Width = 80, IsCancel = true };
        buttons.Children.Add(btnOk);
        buttons.Children.Add(btnCancel);
        panel.Children.Add(buttons);
        dlg.Content = panel;
        dlg.ContentRendered += (_, _) => textBox.Focus();
        return dlg.ShowDialog() == true ? result : null;
    }

    // ── Drag-and-drop materials ───────────────────────────────────────────────

    private void OnMaterialDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects                       = DragDropEffects.Copy;
            TxtDropHint.Visibility          = Visibility.Visible;
            MaterialsDropZone.BorderBrush   = (Brush)FindResource("AccentBrush");
            MaterialsDropZone.BorderThickness = new Thickness(2);
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void OnMaterialDragLeave(object sender, DragEventArgs e)
    {
        TxtDropHint.Visibility            = Visibility.Collapsed;
        MaterialsDropZone.BorderBrush     = Brushes.Transparent;
        MaterialsDropZone.BorderThickness = new Thickness(0);
        e.Handled = true;
    }

    private async void OnMaterialDrop(object sender, DragEventArgs e)
    {
        TxtDropHint.Visibility            = Visibility.Collapsed;
        MaterialsDropZone.BorderBrush     = Brushes.Transparent;
        MaterialsDropZone.BorderThickness = new Thickness(0);

        if (!e.Data.GetDataPresent(DataFormats.FileDrop) || _activeNotebook is null)
        {
            e.Handled = true;
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        SetToolBusy(true, $"⏳ {files.Length} Datei(en) werden geladen…");
        try
        {
            foreach (var filePath in files)
                await UploadFileFromPathAsync(filePath);
        }
        finally
        {
            SetToolBusy(false, string.Empty);
        }
        e.Handled = true;
    }

    // ── Formula extraction ────────────────────────────────────────────────────

    private async void OnExtractFormulasClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null || _activeNotebook.Materials.Count == 0)
        {
            MessageBox.Show("Keine Materialien vorhanden.", "Formeln extrahieren",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetToolBusy(true, "⏳ Formeln werden extrahiert…");
        FormulaPanel.Visibility = Visibility.Collapsed;

        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;

        try
        {
            if (!await _ai.IsOllamaAvailableAsync(ct))
            { OllamaStatusBar.Visibility = Visibility.Visible; return; }

            var ctx = BuildMaterialContext(); // already token-limited
            var prompt = $"Extrahiere alle Formeln. Format pro Formel (Trenner ---):\nLATEX: <LaTeX>\nBESCHREIBUNG: <kurz>\nKATEGORIE: <Thema>\n---\nNur echte Formeln, kein Fließtext. Max 30.\n{ctx}";

            var (provider, model) = await _ai.SelectAsync(AITask.Summarization, ct);
            var response = await GenerateWithCacheAsync(
                "formulas", prompt,
                () => provider.GenerateAsync(model, prompt, ct));

            Formulas.Clear();
            var parsed = ParseFormulas(response);
            foreach (var f in parsed) Formulas.Add(f);

            TxtFormulaCount.Text    = $"{Formulas.Count} Formeln";
            FormulaPanel.Visibility = Formulas.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Persist to DB
            if (UserSession.IsAuthenticated && Formulas.Count > 0)
            {
                using var db = OpenDb();
                db.Formulas.RemoveRange(db.Formulas.Where(f => f.NotebookId == _activeNotebook.Id));
                foreach (var f in Formulas)
                    db.Formulas.Add(new MindForge.Models.FormulaEntry
                    {
                        Id          = Guid.NewGuid(),
                        NotebookId  = _activeNotebook.Id,
                        LaTeX       = f.LaTeX,
                        Description = f.Description,
                        Category    = f.Category,
                        CreatedAt   = DateTime.UtcNow,
                    });
                await db.SaveChangesAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler: {ex.Message}", "Formeln extrahieren",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetToolBusy(false, string.Empty); }
    }

    private static List<FormulaDisplayItem> ParseFormulas(string raw)
    {
        var result = new List<FormulaDisplayItem>();
        foreach (var block in raw.Split(["---"], StringSplitOptions.RemoveEmptyEntries))
        {
            string? latex = null, desc = null, cat = null;
            foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("LATEX:",       StringComparison.OrdinalIgnoreCase)) latex = line[6..].Trim();
                if (line.StartsWith("BESCHREIBUNG:", StringComparison.OrdinalIgnoreCase)) desc = line[13..].Trim();
                if (line.StartsWith("KATEGORIE:",    StringComparison.OrdinalIgnoreCase)) cat  = line[10..].Trim();
            }
            if (!string.IsNullOrWhiteSpace(latex))
                result.Add(new FormulaDisplayItem
                {
                    LaTeX       = latex,
                    Description = desc  ?? string.Empty,
                    Category    = cat   ?? string.Empty,
                });
        }
        return result;
    }

    // ── Audio overview ────────────────────────────────────────────────────────

    private async void OnAudioOverviewClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null) return;

        SetToolBusy(true, "⏳ Audio-Zusammenfassung wird erstellt…");
        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;

        try
        {
            if (!await _ai.IsOllamaAvailableAsync(ct))
            { OllamaStatusBar.Visibility = Visibility.Visible; return; }

            var ctx    = BuildMaterialContext(); // already token-limited
            var lang   = _activeNotebook.Settings.Language;
            var prompt = string.IsNullOrEmpty(ctx)
                ? $"Audio-Zusammenfassung (~250 Wörter, natürliche Sprache, keine Listen) in {lang} zu: {_activeSubject?.Name}"
                : $"Audio-Zusammenfassung (~250 Wörter, natürliche Sprache, keine Listen) in {lang}:\n{ctx}";

            var (provider, model) = await _ai.SelectAsync(AITask.Summarization, ct);
            var summary = await GenerateWithCacheAsync(
                "audio", prompt,
                () => provider.GenerateAsync(model, prompt, ct));

            SetToolBusy(false, string.Empty);
            SetToolBusy(true, "🎙 Audio wird abgespielt…");

            await Task.Run(() =>
            {
                using var synth = new SpeechSynthesizer();
                synth.Rate   = 0;     // normal speed
                synth.Volume = 100;

                // Select voice matching the language setting
                try
                {
                    var voices = synth.GetInstalledVoices();
                    var langCode = lang switch
                    {
                        "English"  => "en",
                        "Français" => "fr",
                        "Español"  => "es",
                        _          => "de",
                    };
                    var voice = voices.FirstOrDefault(v =>
                        v.VoiceInfo.Culture.Name.StartsWith(langCode, StringComparison.OrdinalIgnoreCase)
                        && v.Enabled);
                    if (voice != null)
                        synth.SelectVoice(voice.VoiceInfo.Name);
                }
                catch { /* use default voice */ }

                synth.Speak(summary);
            }, ct);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageBox.Show($"Audio-Fehler: {ex.Message}", "Audio-Überblick",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetToolBusy(false, string.Empty); }
    }

    // ── Smart merge ───────────────────────────────────────────────────────────

    private async void OnSmartMergeClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null || _activeNotebook.Materials.Count == 0)
        {
            MessageBox.Show("Keine Materialien vorhanden.", "Smart Merge",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // ── Build dialog in code (no extra .xaml file needed) ────────────────
        var dlg = new Window
        {
            Title                 = "Smart Merge – Materialien zusammenführen",
            Width                 = 580,
            Height                = 460,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle           = WindowStyle.SingleBorderWindow,
            ResizeMode            = ResizeMode.NoResize,
        };

        var outer = new Grid { Margin = new Thickness(20) };
        outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // header
        outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // checkboxes
        outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // slider block
        outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // buttons

        // Header
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
        header.Children.Add(new TextBlock
        {
            Text = "Materialien für den Merge auswählen:",
            FontWeight = FontWeights.SemiBold, FontSize = 14,
        });
        header.Children.Add(new TextBlock
        {
            Text = "KI führt die gewählten Dokumente in einer einheitlichen Lernunterlage zusammen.",
            FontSize = 12, Opacity = 0.65, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetRow(header, 0);

        // Checkboxes
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 12) };
        var cbPanel = new StackPanel();
        var checkboxes = _activeNotebook.Materials.Select(m =>
        {
            var cb = new System.Windows.Controls.CheckBox
            {
                IsChecked = true,
                Tag       = m,
                Margin    = new Thickness(0, 4, 0, 4),
                Content   = new TextBlock { Text = $"{m.Icon}  {m.Name}", TextTrimming = TextTrimming.CharacterEllipsis },
            };
            return cb;
        }).ToList();
        foreach (var cb in checkboxes) cbPanel.Children.Add(cb);
        scroll.Content = cbPanel;
        Grid.SetRow(scroll, 1);

        // Compression slider
        var sliderBlock = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        var sliderTitle = new TextBlock { Text = "Detailgrad: 50%", FontWeight = FontWeights.SemiBold };
        var slider = new Slider { Minimum = 10, Maximum = 100, Value = 50, TickFrequency = 10, IsSnapToTickEnabled = true };
        slider.ValueChanged += (s, ev) => sliderTitle.Text = $"Detailgrad: {(int)slider.Value}%";
        var sliderHint = new TextBlock
        {
            Text = "10% = Nur Formeln & Definitionen  |  50% = + kurze Erklärungen  |  100% = Vollständige Herleitungen",
            FontSize = 11, Opacity = 0.6, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap,
        };
        sliderBlock.Children.Add(sliderTitle);
        sliderBlock.Children.Add(slider);
        sliderBlock.Children.Add(sliderHint);
        Grid.SetRow(sliderBlock, 2);

        // Buttons
        var btnRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        bool confirmed = false;
        var btnCancel = new Button
        {
            Content = "Abbrechen", IsCancel = true, Width = 90,
            Padding = new Thickness(0, 8, 0, 8), Margin = new Thickness(0, 0, 10, 0),
        };
        var btnGo = new Button
        {
            Content = "🧬  Generieren", IsDefault = true, Width = 120,
            Padding = new Thickness(0, 8, 0, 8),
        };
        btnGo.Click    += (_, _) => { confirmed = true; dlg.DialogResult = true; };
        btnCancel.Click += (_, _) => dlg.DialogResult = false;
        btnRow.Children.Add(btnCancel);
        btnRow.Children.Add(btnGo);
        Grid.SetRow(btnRow, 3);

        outer.Children.Add(header);
        outer.Children.Add(scroll);
        outer.Children.Add(sliderBlock);
        outer.Children.Add(btnRow);
        dlg.Content = outer;

        if (dlg.ShowDialog() != true || !confirmed) return;

        var selected = checkboxes
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (NotebookMaterialItem)cb.Tag)
            .ToList();

        if (selected.Count == 0) return;

        var compression = (int)slider.Value;

        // ── Generate ─────────────────────────────────────────────────────────
        SetToolBusy(true, "⏳ Merge wird generiert…");
        ToolResultPanel.Visibility = Visibility.Visible;
        TxtToolTitle.Text = "🧬 Smart Merge";
        TxtToolResult.Text = "";

        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;

        try
        {
            if (!await _ai.IsOllamaAvailableAsync(ct))
            { OllamaStatusBar.Visibility = Visibility.Visible; return; }

            var detailDesc = compression switch
            {
                <= 30 => "Nur Formeln, Definitionen, Schlüsselbegriffe.",
                <= 60 => "Formeln + Kurzerklärungen (2-3 Sätze je Konzept).",
                _     => "Vollständig mit Herleitungen und Beispielen.",
            };

            // Token budget per material: spread evenly across selected materials
            // Total budget 2000 tokens → split by count; at minimum 200 tokens each
            int tokenPerMat = Math.Max(200, 2000 / selected.Count);
            var matCtx = string.Join("\n", selected.Select(m =>
                $"[{m.Name}] {TruncateToTokens(m.Content, tokenPerMat)}"));

            var prompt = $"Einheitliches Lerndokument. Detailgrad {compression}%: {detailDesc}\n" +
                         "Format: # Kapitel, ## Abschnitte, $$Formeln$$, Bullet-Points.\n" +
                         $"Materialien:\n{matCtx}";

            var (provider, model) = await _ai.SelectAsync(AITask.StudyGuide, ct);
            TxtToolResult.Text = await GenerateWithCacheAsync(
                "smartmerge", prompt,
                () => provider.GenerateAsync(model, prompt, ct));
        }
        catch (OperationCanceledException) { TxtToolResult.Text = "Abgebrochen."; }
        catch (Exception ex) { TxtToolResult.Text = $"⚠️ Fehler: {ex.Message}"; }
        finally { SetToolBusy(false, string.Empty); }
    }

    // ── Notebook snapshots (version control) ──────────────────────────────────

    private async void OnSaveSnapshotClick(object sender, RoutedEventArgs e)
    {
        if (!UserSession.IsAuthenticated || _activeNotebook is null) return;

        var label = ShowInputDialog(
            "Version speichern",
            "Versionsbezeichnung (optional):") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(label))
            label = $"Snapshot {DateTime.Now:dd.MM.yyyy HH:mm}";

        using var db = OpenDb();
        var snapshot = new MindForge.Models.NotebookSnapshot
        {
            Id            = Guid.NewGuid(),
            NotebookId    = _activeNotebook.Id,
            CreatedAt     = DateTime.UtcNow,
            Label         = label,
            MaterialCount = _activeNotebook.Materials.Count,
            ChatCount     = _activeNotebook.ChatHistory.Count,
            MaterialsJson = JsonSerializer.Serialize(
                _activeNotebook.Materials.Select(m => new { m.Id, m.Name, m.Content, m.Icon })),
            ChatJson = JsonSerializer.Serialize(
                _activeNotebook.ChatHistory.Select(c => new { c.IsUser, c.Content, c.Time })),
        };
        db.NotebookSnapshots.Add(snapshot);
        await db.SaveChangesAsync();

        MessageBox.Show($"Version gespeichert: \"{label}\"",
            "Snapshot", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void OnViewHistoryClick(object sender, RoutedEventArgs e)
    {
        if (!UserSession.IsAuthenticated || _activeNotebook is null) return;

        using var db = OpenDb();
        var snapshots = await db.NotebookSnapshots
            .Where(s => s.NotebookId == _activeNotebook.Id)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        if (snapshots.Count == 0)
        {
            MessageBox.Show("Keine gespeicherten Versionen.", "Versionshistorie",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Build history dialog in code
        var dlg = new Window
        {
            Title                 = "Versionshistorie",
            Width                 = 520,
            Height                = 400,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle           = WindowStyle.SingleBorderWindow,
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock
        {
            Text       = "Gespeicherte Versionen — klicke eine an, um sie wiederherzustellen:",
            FontWeight = FontWeights.SemiBold,
            Margin     = new Thickness(0, 0, 0, 12),
        });

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = 280 };
        var listPanel = new StackPanel();

        foreach (var snap in snapshots)
        {
            var row = new System.Windows.Controls.Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush     = Brushes.LightGray,
                Padding         = new Thickness(0, 8, 0, 8),
            };
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = snap.Label, FontWeight = FontWeights.SemiBold });
            info.Children.Add(new TextBlock
            {
                Text     = $"{snap.CreatedAt.ToLocalTime():dd.MM.yyyy HH:mm}  •  {snap.MaterialCount} Materialien, {snap.ChatCount} Nachrichten",
                FontSize = 11, Opacity = 0.6,
            });

            var btnRestore = new Button
            {
                Content = "Wiederherstellen",
                Tag     = snap,
                Padding = new Thickness(10, 4, 10, 4),
            };
            btnRestore.Click += async (s, ev) =>
            {
                if (s is Button { Tag: MindForge.Models.NotebookSnapshot selected })
                {
                    dlg.Close();
                    await RestoreSnapshotAsync(selected);
                }
            };

            Grid.SetColumn(btnRestore, 1);
            rowGrid.Children.Add(info);
            rowGrid.Children.Add(btnRestore);
            row.Child = rowGrid;
            listPanel.Children.Add(row);
        }

        scroll.Content = listPanel;
        panel.Children.Add(scroll);
        dlg.Content = panel;
        dlg.ShowDialog();
    }

    private async Task RestoreSnapshotAsync(MindForge.Models.NotebookSnapshot snap)
    {
        if (_activeNotebook is null) return;

        try
        {
            // Restore materials (in-memory only — DB materials stay as-is)
            var mats = JsonSerializer.Deserialize<List<dynamic>>(snap.MaterialsJson);
            // We keep existing DB materials but update the in-memory view
            // A proper restore would also write to DB — simplified version restores chat

            // Restore chat
            using var db = OpenDb();
            var chatItems = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(snap.ChatJson) ?? [];

            _activeNotebook.ChatHistory.Clear();
            foreach (var item in chatItems)
            {
                _activeNotebook.ChatHistory.Add(new NotebookChatMsg
                {
                    IsUser  = item.TryGetValue("IsUser",  out var u)  && u.GetBoolean(),
                    Content = item.TryGetValue("Content", out var c)  ? c.GetString() ?? "" : "",
                    Time    = item.TryGetValue("Time",    out var t)  ? t.GetString() ?? "" : "",
                });
            }

            MessageBox.Show($"Version \"{snap.Label}\" wurde wiederhergestellt.\n" +
                $"Chat: {chatItems.Count} Nachrichten geladen.",
                "Snapshot wiederhergestellt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Wiederherstellung fehlgeschlagen: {ex.Message}",
                "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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

    // Each prompt builder uses a token-limited context (≤1500 tokens) via BuildMaterialContext().
    // The context is already pruned there; these prompts stay compact.

    private string BuildSummaryPrompt()
    {
        var ctx = BuildMaterialContext();
        return string.IsNullOrEmpty(ctx)
            ? $"Zusammenfassung (Bullet Points, max 300 Wörter) zu: {_activeSubject?.Name}"
            : $"Zusammenfassung (Bullet Points, max 300 Wörter):\n{ctx}";
    }

    private string BuildELI5Prompt()
    {
        var ctx = BuildMaterialContext();
        return string.IsNullOrEmpty(ctx)
            ? $"Erkläre '{_activeSubject?.Name}' für Anfänger mit Alltagsbeispielen."
            : $"Erkläre einfach, ohne Fachbegriffe. Mit Alltagsbeispielen:\n{ctx}";
    }

    private string BuildStudyGuidePrompt()
    {
        var ctx = BuildMaterialContext();
        var cmd = "Lernleitfaden auf Deutsch: 1.Kernkonzepte 2.Definitionen 3.Prüfungsfragen+Antworten 4.Zusammenfassung";
        return string.IsNullOrEmpty(ctx) ? $"{cmd}\nThema: {_activeSubject?.Name}" : $"{cmd}\n{ctx}";
    }

    private string BuildQuizPrompt()
    {
        var ctx = BuildMaterialContext();
        var cmd = "5 Multiple-Choice-Fragen auf Deutsch. Format: Frage / A) B) C) D) / Richtig: X";
        return string.IsNullOrEmpty(ctx) ? $"{cmd}\nThema: {_activeSubject?.Name}" : $"{cmd}\n{ctx}";
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
            var prompt = promptFn();
            var (provider, model) = await _ai.SelectAsync(task, ct);
            TxtToolResult.Text = await GenerateWithCacheAsync(
                task.ToString(), prompt,
                () => provider.GenerateAsync(model, prompt, ct));
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
                ? $"8 Lernkarten zu '{_activeSubject?.Name}'. Format (--- als Trenner):\nFRAGE: [Frage]\nANTWORT: [Antwort]\n---"
                : $"8 Lernkarten aus diesen Materialien:\nFRAGE: [Frage]\nANTWORT: [Antwort]\n---\n{ctx}";

            var (provider, model) = await _ai.SelectAsync(AITask.StudyGuide, ct);
            var response = await GenerateWithCacheAsync(
                "flashcards", prompt,
                () => provider.GenerateAsync(model, prompt, ct));

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
        FlashcardFront.Visibility  = card.IsFlipped ? Visibility.Collapsed : Visibility.Visible;
        FlashcardBack.Visibility   = card.IsFlipped ? Visibility.Visible   : Visibility.Collapsed;
        SrsRatingPanel.Visibility  = card.IsFlipped ? Visibility.Visible   : Visibility.Collapsed;
        if (!card.IsFlipped) TxtSrsNextReview.Text = "";
    }

    // ── SRS: SM-2 algorithm ───────────────────────────────────────────────────

    private void OnSrsRate1(object sender, RoutedEventArgs e) => ApplySRS(1);
    private void OnSrsRate2(object sender, RoutedEventArgs e) => ApplySRS(2);
    private void OnSrsRate3(object sender, RoutedEventArgs e) => ApplySRS(3);
    private void OnSrsRate4(object sender, RoutedEventArgs e) => ApplySRS(4);

    private void ApplySRS(int quality) // 1=Again  2=Hard  3=Good  4=Easy
    {
        if (_activeNotebook is null || _activeNotebook.Flashcards.Count == 0) return;
        var card = _activeNotebook.Flashcards[_flashcardIndex];

        // Map 1-4 to SM-2 quality scale 0-5
        int q = quality switch { 1 => 0, 2 => 3, 3 => 4, 4 => 5, _ => 3 };

        // Easiness update
        card.Easiness = Math.Max(1.3, card.Easiness + 0.1 - (5 - q) * (0.08 + (5 - q) * 0.02));
        card.ReviewCount++;

        // Interval update
        if (q < 3)
        {
            card.SrsInterval = 0;
            card.NextReview  = DateTime.Now.AddMinutes(10);
        }
        else
        {
            card.SrsInterval = card.SrsInterval switch
            {
                0 => 1,
                1 => 6,
                _ => (int)Math.Round(card.SrsInterval * card.Easiness)
            };
            card.NextReview = DateTime.Now.AddDays(card.SrsInterval);
        }

        // Display next review
        var nextStr = card.NextReview!.Value.Date == DateTime.Today
            ? "Heute nochmal (in 10 min)"
            : card.NextReview.Value.Date == DateTime.Today.AddDays(1)
                ? "Morgen"
                : $"In {card.SrsInterval} Tag(en)";
        TxtSrsNextReview.Text = $"Nächste Wiederholung: {nextStr}";

        // Auto-advance after 1.5 s
        Dispatcher.InvokeAsync(async () =>
        {
            await Task.Delay(1500);
            if (_activeNotebook.Flashcards.Count > 0)
            {
                _activeNotebook.Flashcards[_flashcardIndex].IsFlipped = false;
                _flashcardIndex = (_flashcardIndex + 1) % _activeNotebook.Flashcards.Count;
                ShowFlashcard();
            }
        });
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

    // ══════════════════════════════════════════════════════════════════════════
    // v5.0.0 — New feature handlers
    // ══════════════════════════════════════════════════════════════════════════

    // ── Export tool result to .md ─────────────────────────────────────────────
    private void OnExportResultMdClick(object sender, RoutedEventArgs e)
    {
        var text = TxtToolResult.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Kein Ergebnis zum Exportieren.", "Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var rawTitle = TxtToolTitle.Text.Trim().TrimStart('#').Trim();
        // Strip any leading non-letter/non-digit characters (e.g. emoji prefixes)
        while (rawTitle.Length > 0 && !char.IsLetterOrDigit(rawTitle[0]))
            rawTitle = rawTitle.Length > 1 ? rawTitle[1..] : "";
        rawTitle = rawTitle.Trim();
        if (string.IsNullOrEmpty(rawTitle)) rawTitle = "Ergebnis";

        var dlg = new SaveFileDialog
        {
            Title      = "Ergebnis als Markdown speichern",
            FileName   = $"{SanitizeFileName(rawTitle)}_{DateTime.Now:yyyy-MM-dd}.md",
            Filter     = "Markdown|*.md|Textdatei|*.txt|Alle Dateien|*.*",
            DefaultExt = ".md",
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine($"# {rawTitle}");
        sb.AppendLine($"> Generiert von MindForge am {DateTime.Now:dd.MM.yyyy HH:mm}");
        if (_activeNotebook != null)
            sb.AppendLine($"> Notizbuch: {_activeNotebook.Name} | Fach: {_activeSubject?.Name ?? "–"}");
        sb.AppendLine();
        sb.AppendLine(text);

        File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                { FileName = dlg.FileName, UseShellExecute = true });
        }
        catch { /* file open is best-effort */ }
    }

    // ── Save tool result as new notebook material ─────────────────────────────
    private async void OnSaveResultAsMaterialClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null) return;
        var text = TxtToolResult.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            MessageBox.Show("Kein Ergebnis zum Speichern.", "Material",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var rawTitle = TxtToolTitle.Text.Trim();
        if (string.IsNullOrEmpty(rawTitle)) rawTitle = "KI-Ergebnis";
        var name = $"🤖 {rawTitle} ({DateTime.Now:dd.MM. HH:mm})";

        await AddTextMaterialAsync(name, text, "🤖");
    }

    /// <summary>Create a new material from plain text without going through the file-upload flow.</summary>
    private async Task AddTextMaterialAsync(string name, string content, string icon = "📝")
    {
        if (_activeNotebook is null) return;

        if (content.Length > 50_000) content = content[..50_000] + "\n[… Inhalt gekürzt …]";

        var id = await SaveMaterialToDbAsync(name, content, MaterialFormat.Text, string.Empty);
        var matItem = new NotebookMaterialItem { Id = id, Name = name, Content = content, Icon = icon };
        _activeNotebook.Materials.Add(matItem);
        RefreshDetailProgress();
        await SaveNotebookProgressAsync();

        _ = Task.Run(() => _rag.IndexMaterialAsync(id, _activeNotebook.Id, name, content));

        MessageBox.Show($"✅ Als Material gespeichert: \"{name}\"",
            "Material gespeichert", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Flashcard statistics dialog ───────────────────────────────────────────
    private void OnFlashcardStatsClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null || _activeNotebook.Flashcards.Count == 0)
        {
            MessageBox.Show("Keine Lernkarten vorhanden. Generiere zuerst Lernkarten.",
                "Statistiken", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var cards = _activeNotebook.Flashcards;
        var now   = DateTime.Now;

        int total    = cards.Count;
        int due      = cards.Count(c => c.NextReview == null || c.NextReview <= now);
        int learning = cards.Count(c => c.SrsInterval <= 7);
        int review   = cards.Count(c => c.SrsInterval is > 7 and <= 30);
        int mastered = cards.Count(c => c.SrsInterval > 30 && c.Easiness >= 2.5);
        double avgEase = cards.Average(c => c.Easiness);

        var nextDue = cards
            .Where(c => c.NextReview > now)
            .OrderBy(c => c.NextReview)
            .FirstOrDefault();

        var dlg = new Window
        {
            Title                 = "📊 Lernkarten-Statistiken",
            Width                 = 380,
            Height                = 360,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode            = ResizeMode.NoResize,
            WindowStyle           = WindowStyle.SingleBorderWindow,
        };

        var sp = new StackPanel { Margin = new Thickness(24) };

        sp.Children.Add(new TextBlock
        {
            Text       = $"🎴 {_activeNotebook.Name}",
            FontSize   = 16,
            FontWeight = FontWeights.Bold,
            Margin     = new Thickness(0, 0, 0, 16),
        });

        void AddStat(string label, string value, string hexColor)
        {
            var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 });
            var badge = new System.Windows.Controls.Border
            {
                Background    = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(hexColor)),
                CornerRadius  = new CornerRadius(5),
                Padding       = new Thickness(10, 3, 10, 3),
                Child         = new TextBlock { Text = value, Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13 },
            };
            Grid.SetColumn(badge, 1);
            row.Children.Add(badge);
            sp.Children.Add(row);
        }

        AddStat("Gesamt",            $"{total}",                  "#607D8B");
        AddStat("Fällig heute",      $"{due}",                    "#F44336");
        AddStat("Im Lernmodus",      $"{learning}",               "#FF9800");
        AddStat("Im Wiederholungsmodus", $"{review}",             "#2196F3");
        AddStat("Beherrscht",        $"{mastered}",               "#4CAF50");
        AddStat("∅ Leichtigkeit",    $"{avgEase:F2}",             "#9C27B0");

        sp.Children.Add(new Separator { Margin = new Thickness(0, 14, 0, 14) });

        if (nextDue != null)
            sp.Children.Add(new TextBlock
            {
                Text       = $"⏳ Nächste Wiederholung: {nextDue.NextReview:dd.MM.yyyy}",
                FontSize   = 12,
                Foreground = Brushes.Gray,
            });
        else
            sp.Children.Add(new TextBlock
            {
                Text       = "✅ Alle Karten sind für heute erledigt!",
                FontSize   = 12,
                Foreground = new SolidColorBrush(Colors.Green),
            });

        dlg.Content = sp;
        dlg.ShowDialog();
    }

    // ── ZIP export ────────────────────────────────────────────────────────────
    private async void OnExportZipClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null) return;

        var dlg = new SaveFileDialog
        {
            Title      = "Notizbuch als ZIP exportieren",
            FileName   = $"{SanitizeFileName(_activeNotebook.Name)}_Paket.zip",
            Filter     = "ZIP-Archiv|*.zip|Alle Dateien|*.*",
            DefaultExt = ".zip",
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            if (File.Exists(dlg.FileName)) File.Delete(dlg.FileName);

            using var zip = System.IO.Compression.ZipFile.Open(
                dlg.FileName, System.IO.Compression.ZipArchiveMode.Create);

            // metadata.json
            var meta = JsonSerializer.Serialize(new
            {
                MindForgeVersion = "5.0",
                NotebookId       = _activeNotebook.Id.ToString(),
                Name             = _activeNotebook.Name,
                Subject          = _activeSubject?.Name ?? string.Empty,
                ExportedAt       = DateTime.UtcNow.ToString("O"),
                Language         = _activeNotebook.Settings.Language,
                LearningLevel    = _activeNotebook.Settings.LearningLevel,
                MaterialCount    = _activeNotebook.Materials.Count,
                ChatCount        = _activeNotebook.ChatHistory.Count,
                FlashcardCount   = _activeNotebook.Flashcards.Count,
            }, new JsonSerializerOptions { WriteIndented = true });

            var metaEntry = zip.CreateEntry("metadata.json");
            await using (var w = new StreamWriter(metaEntry.Open()))
                await w.WriteAsync(meta);

            // materials/<name>.txt
            foreach (var mat in _activeNotebook.Materials)
            {
                var entry = zip.CreateEntry($"materials/{SanitizeFileName(mat.Name)}.txt");
                await using var w = new StreamWriter(entry.Open(), System.Text.Encoding.UTF8);
                await w.WriteLineAsync($"# {mat.Name}");
                await w.WriteLineAsync();
                await w.WriteAsync(mat.Content);
            }

            // chat.md
            if (_activeNotebook.ChatHistory.Count > 0)
            {
                var chatEntry = zip.CreateEntry("chat.md");
                await using var w = new StreamWriter(chatEntry.Open(), System.Text.Encoding.UTF8);
                await w.WriteLineAsync($"# Chat: {_activeNotebook.Name}");
                await w.WriteLineAsync($"> Exportiert: {DateTime.Now:dd.MM.yyyy HH:mm}");
                await w.WriteLineAsync();
                foreach (var msg in _activeNotebook.ChatHistory)
                {
                    await w.WriteLineAsync(msg.IsUser ? $"**Du** _{msg.Time}_" : $"**KI** _{msg.Time}_");
                    await w.WriteLineAsync(msg.Content);
                    await w.WriteLineAsync();
                }
            }

            // flashcards.md
            if (_activeNotebook.Flashcards.Count > 0)
            {
                var fcEntry = zip.CreateEntry("flashcards.md");
                await using var w = new StreamWriter(fcEntry.Open(), System.Text.Encoding.UTF8);
                await w.WriteLineAsync($"# Lernkarten: {_activeNotebook.Name}");
                await w.WriteLineAsync();
                foreach (var card in _activeNotebook.Flashcards)
                {
                    await w.WriteLineAsync($"**FRAGE:** {card.Question}");
                    await w.WriteLineAsync($"**ANTWORT:** {card.Answer}");
                    await w.WriteLineAsync($"_Leichtigkeit: {card.Easiness:F2} | Intervall: {card.SrsInterval}d_");
                    await w.WriteLineAsync();
                    await w.WriteLineAsync("---");
                    await w.WriteLineAsync();
                }
            }

            MessageBox.Show($"✅ Notizbuch exportiert!\n\n{dlg.FileName}",
                "ZIP Export", MessageBoxButton.OK, MessageBoxImage.Information);

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{dlg.FileName}\"");
            }
            catch { /* best-effort */ }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export-Fehler: {ex.Message}", "ZIP Export",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── ZIP import ────────────────────────────────────────────────────────────
    private async void OnImportZipClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null) return;

        var dlg = new OpenFileDialog
        {
            Title       = "MindForge-Notizbuchpaket öffnen",
            Filter      = "ZIP-Archiv|*.zip|Alle Dateien|*.*",
            Multiselect = false,
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(dlg.FileName);

            var metaEntry = zip.GetEntry("metadata.json");
            if (metaEntry == null)
            {
                MessageBox.Show(
                    "Diese ZIP-Datei ist kein gültiges MindForge-Notizbuchpaket.\n(metadata.json fehlt)",
                    "Import fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string metaJson;
            using (var r = new StreamReader(metaEntry.Open()))
                metaJson = await r.ReadToEndAsync();

            using var metaDoc  = JsonDocument.Parse(metaJson);
            var notebookName   = metaDoc.RootElement.TryGetProperty("Name", out var n)
                ? n.GetString() ?? "Importiert" : "Importiert";
            var materialCount  = metaDoc.RootElement.TryGetProperty("MaterialCount", out var mc)
                ? mc.GetInt32() : 0;

            var confirm = MessageBox.Show(
                $"Notizbuch \"{notebookName}\" importieren?\n" +
                $"{materialCount} Materialien werden in das aktuelle Notizbuch eingefügt.",
                "ZIP Import", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            int imported = 0;
            foreach (var entry in zip.Entries
                .Where(e => e.FullName.StartsWith("materials/") && e.Name.Length > 0))
            {
                string content;
                using (var r = new StreamReader(entry.Open(), System.Text.Encoding.UTF8))
                    content = await r.ReadToEndAsync();

                // Strip "# Name\n\n" header if present
                var lines   = content.Split('\n');
                var matName = lines[0].TrimStart('#').Trim();
                if (string.IsNullOrWhiteSpace(matName))
                    matName = Path.GetFileNameWithoutExtension(entry.Name);
                var matContent = string.Join('\n', lines.Skip(2)).Trim();

                await AddTextMaterialAsync(matName, matContent, "📥");
                imported++;
            }

            MessageBox.Show($"✅ {imported} Materialien importiert.",
                "ZIP Import", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import-Fehler: {ex.Message}", "ZIP Import",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Interactive Quiz Mode ─────────────────────────────────────────────────
    private async void OnInteractiveQuizClick(object sender, RoutedEventArgs e)
    {
        if (_activeNotebook is null) return;

        SetToolBusy(true, "⏳ Quiz wird generiert…");
        _aiCts = new CancellationTokenSource();
        var ct = _aiCts.Token;

        string raw;
        try
        {
            if (!await _ai.IsOllamaAvailableAsync(ct))
            {
                OllamaStatusBar.Visibility = Visibility.Visible;
                SetToolBusy(false, string.Empty);
                return;
            }

            var ctx    = BuildMaterialContext();
            var prompt = string.IsNullOrEmpty(ctx)
                ? $"10 Multiple-Choice-Fragen zu '{_activeSubject?.Name}' auf Deutsch. Format:\nQ: [Frage]\nA) [Option] B) [Option] C) [Option] D) [Option]\nRICHTIG: [A/B/C/D]\n---"
                : $"10 Multiple-Choice-Fragen aus diesen Materialien auf Deutsch:\nQ: [Frage]\nA) [Option] B) [Option] C) [Option] D) [Option]\nRICHTIG: [A/B/C/D]\n---\n{ctx}";

            var (provider, model) = await _ai.SelectAsync(AITask.Summarization, ct);
            raw = await GenerateWithCacheAsync("interactivequiz", prompt,
                () => provider.GenerateAsync(model, prompt, ct));
        }
        catch (OperationCanceledException) { SetToolBusy(false, string.Empty); return; }
        catch (Exception ex)
        {
            SetToolBusy(false, string.Empty);
            MessageBox.Show($"Fehler: {ex.Message}", "Quiz", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        finally { SetToolBusy(false, string.Empty); }

        var questions = ParseQuizQuestions(raw);
        if (questions.Count == 0)
        {
            MessageBox.Show("Keine Fragen erkannt. Bitte versuche es erneut.",
                "Quiz", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ShowInteractiveQuiz(questions);
    }

    private record QuizQuestion(string Text, string A, string B, string C, string D, char Correct);

    private static List<QuizQuestion> ParseQuizQuestions(string raw)
    {
        var questions = new List<QuizQuestion>();
        foreach (var block in raw.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries))
        {
            string? q = null, a = null, b = null, c = null, d = null;
            char correct = 'A';

            foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var l = line.Trim();
                if (l.StartsWith("Q:", StringComparison.OrdinalIgnoreCase))
                    q = l[2..].Trim();
                else if (l.StartsWith("A)", StringComparison.OrdinalIgnoreCase))
                    a = ParseOption(l);
                else if (l.StartsWith("B)", StringComparison.OrdinalIgnoreCase))
                    b = ParseOption(l);
                else if (l.StartsWith("C)", StringComparison.OrdinalIgnoreCase))
                    c = ParseOption(l);
                else if (l.StartsWith("D)", StringComparison.OrdinalIgnoreCase))
                    d = ParseOption(l);
                else if (l.StartsWith("RICHTIG:", StringComparison.OrdinalIgnoreCase))
                {
                    var ans = l[8..].Trim().ToUpper();
                    if (ans.Length > 0 && ans[0] is 'A' or 'B' or 'C' or 'D')
                        correct = ans[0];
                }
            }

            if (!string.IsNullOrWhiteSpace(q) && a != null && b != null && c != null && d != null)
                questions.Add(new QuizQuestion(q, a, b, c, d, correct));
        }
        return questions;

        static string ParseOption(string line)
        {
            var idx = line.IndexOf(')');
            return idx >= 0 ? line[(idx + 1)..].Trim() : line[2..].Trim();
        }
    }

    private void ShowInteractiveQuiz(List<QuizQuestion> questions)
    {
        int currentIndex = 0;
        int score        = 0;
        char? chosen     = null;
        bool answered    = false;

        // ── Build dialog ──────────────────────────────────────────────────────
        var dlg = new Window
        {
            Title                 = "📝 Quiz-Modus",
            Width                 = 620,
            Height                = 480,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode            = ResizeMode.CanResize,
            WindowStyle           = WindowStyle.SingleBorderWindow,
            MinWidth              = 480,
            MinHeight             = 400,
        };

        // Layout
        var root = new Grid { Margin = new Thickness(24) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // progress
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // question
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // options
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // feedback
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });      // nav

        // Progress bar + counter
        var progRow = new Grid { Margin = new Thickness(0, 0, 0, 16) };
        progRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        progRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var progBar = new ProgressBar { Height = 6, Minimum = 0, Maximum = questions.Count, Value = 0 };
        var progLbl = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0), FontSize = 12 };
        Grid.SetColumn(progLbl, 1);
        progRow.Children.Add(progBar);
        progRow.Children.Add(progLbl);
        Grid.SetRow(progRow, 0);
        root.Children.Add(progRow);

        // Question text
        var questionText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 15,
            FontWeight   = FontWeights.SemiBold,
            Margin       = new Thickness(0, 0, 0, 20),
        };
        Grid.SetRow(questionText, 1);
        root.Children.Add(questionText);

        // Option buttons
        var optionsPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(optionsPanel, 2);
        root.Children.Add(optionsPanel);

        // Feedback label
        var feedbackText = new TextBlock
        {
            FontSize     = 14,
            FontWeight   = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            Margin       = new Thickness(0, 12, 0, 0),
            Visibility   = Visibility.Collapsed,
        };
        Grid.SetRow(feedbackText, 3);
        root.Children.Add(feedbackText);

        // Navigation row
        var navRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        navRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        navRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var scoreLbl = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
        var nextBtn  = new Button
        {
            Content   = "Weiter →",
            Padding   = new Thickness(20, 8, 20, 8),
            IsEnabled = false,
        };
        Grid.SetColumn(nextBtn, 1);
        navRow.Children.Add(scoreLbl);
        navRow.Children.Add(nextBtn);
        Grid.SetRow(navRow, 4);
        root.Children.Add(navRow);

        dlg.Content = root;

        // ── Render question ───────────────────────────────────────────────────
        void RenderQuestion()
        {
            var q       = questions[currentIndex];
            chosen      = null;
            answered    = false;
            nextBtn.IsEnabled  = false;
            feedbackText.Visibility = Visibility.Collapsed;

            progBar.Value = currentIndex;
            progLbl.Text  = $"{currentIndex + 1} / {questions.Count}";
            scoreLbl.Text = $"Punkte: {score}";
            questionText.Text = $"Frage {currentIndex + 1}: {q.Text}";

            optionsPanel.Children.Clear();
            foreach (var (letter, text) in new[] { ('A', q.A), ('B', q.B), ('C', q.C), ('D', q.D) })
            {
                var ltr   = letter; // capture for lambda
                var btn = new Button
                {
                    Content             = $"  {ltr})  {text}",
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Margin              = new Thickness(0, 0, 0, 8),
                    Padding             = new Thickness(14, 10, 14, 10),
                    FontSize            = 13,
                    BorderThickness     = new Thickness(1),
                    Tag                 = ltr,
                };

                btn.Click += (_, _) =>
                {
                    if (answered) return;
                    answered = true;
                    chosen   = ltr;

                    bool correct = ltr == q.Correct;
                    if (correct) score++;

                    // Colour the buttons
                    foreach (Button ob in optionsPanel.Children)
                    {
                        var ol = (char)ob.Tag;
                        ob.IsEnabled = false;
                        if (ol == q.Correct)
                            ob.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x43, 0xA0, 0x47)); // green
                        else if (ol == ltr && !correct)
                            ob.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x39, 0x35)); // red
                    }

                    feedbackText.Text       = correct ? "✅ Richtig!" : $"❌ Falsch. Richtige Antwort: {q.Correct}";
                    feedbackText.Foreground = correct ? Brushes.Green : Brushes.Crimson;
                    feedbackText.Visibility = Visibility.Visible;
                    nextBtn.IsEnabled = true;
                };

                optionsPanel.Children.Add(btn);
            }
        }

        nextBtn.Click += (_, _) =>
        {
            currentIndex++;
            if (currentIndex >= questions.Count)
            {
                // Results screen
                progBar.Value    = questions.Count;
                progLbl.Text     = $"{questions.Count} / {questions.Count}";
                questionText.Text = $"Quiz abgeschlossen!";
                optionsPanel.Children.Clear();
                feedbackText.Visibility = Visibility.Collapsed;

                double pct = questions.Count > 0 ? score * 100.0 / questions.Count : 0;
                var grade = pct >= 90 ? "🏆 Ausgezeichnet!" :
                            pct >= 70 ? "✅ Gut gemacht!"   :
                            pct >= 50 ? "📖 Weiter lernen!" :
                                        "📚 Noch mehr üben!";

                scoreLbl.Text      = $"Ergebnis: {score} / {questions.Count}  ({pct:F0}%)  {grade}";
                nextBtn.Content    = "Schließen";
                nextBtn.IsEnabled  = true;
                nextBtn.Click     += (_, _) => dlg.Close();
            }
            else
            {
                RenderQuestion();
            }
        };

        RenderQuestion();
        dlg.ShowDialog();
    }

    // ── Filename sanitizer (also used for ZIP) ────────────────────────────────
    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Length > 60 ? name[..60] : name;
    }
}
