using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Helpers;
using MindForge.Models;
using MindForge.Services.AI;
using MindForge.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MindForge.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly MindForgeDbContext _db;
    private readonly IChatService      _chat;
    private readonly AISelector        _ai;
    private CancellationTokenSource?   _cts;

    // Elapsed-time tracking on the UI thread
    private readonly Stopwatch        _stopwatch    = new();
    private readonly DispatcherTimer  _elapsedTimer;
    private int _sessionTokens;

    // ── Observable properties ─────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ChatMessage> _messages         = new();
    [ObservableProperty] private ObservableCollection<Material>    _availableMaterials = new();
    [ObservableProperty] private ObservableCollection<Material>    _contextMaterials   = new();
    [ObservableProperty] private ObservableCollection<string>      _availableModels    = new();

    [ObservableProperty] private string  _selectedModel            = string.Empty;
    [ObservableProperty] private string  _inputText                = string.Empty;
    [ObservableProperty] private bool    _isBusy;
    [ObservableProperty] private string  _streamingResponse        = string.Empty;
    [ObservableProperty] private bool    _isStreaming;
    [ObservableProperty] private string  _elapsedTime              = "0.0s";
    [ObservableProperty] private string  _tokenDisplay             = "0 tokens";
    [ObservableProperty] private int     _selectedPersonalityIndex;

    // ── Extended settings ─────────────────────────────────────────────────────
    [ObservableProperty] private bool   _useExamples;
    [ObservableProperty] private bool   _useStepByStep;
    [ObservableProperty] private bool   _useCitations;
    [ObservableProperty] private string _customPrompt  = string.Empty;
    [ObservableProperty] private double _temperature   = 0.7;

    // ── Personality system ────────────────────────────────────────────────────

    private static readonly string[] _personalityKeys =
    [
        "neutral", "patient", "strict", "enthusiastic",
        "socratic", "genius", "humorous", "annoyed"
    ];

    /// <summary>Displayed in the UI ComboBox; index matches _personalityKeys.</summary>
    public IReadOnlyList<string> PersonalityLabels { get; } =
    [
        "🎓 Neutraler Tutor",
        "😊 Geduldiger Erklärer",
        "📐 Strenger Professor",
        "🎉 Begeisterter Mitschüler",
        "🤔 Sokratischer Fragesteller",
        "🧬 Hochintelligenter Mentor",
        "😄 Humorvoller Coach",
        "😤 Genervter Professor",
    ];

    private static readonly Dictionary<string, string> _personalityPrompts = new()
    {
        ["neutral"]      = "Du bist ein hilfreicher, neutraler KI-Tutor. " +
                           "Antworte präzise, klar und auf Deutsch.",

        ["patient"]      = "Du bist ein außerordentlich geduldiger Lehrer. Erkläre Konzepte " +
                           "langsam und gründlich, wiederhole wenn nötig, und ermutige " +
                           "den Lernenden ständig. Antworte auf Deutsch.",

        ["strict"]       = "Du bist ein strenger, aber fairer Professor. Du hast hohe " +
                           "Erwartungen, akzeptierst keine ungenauen Antworten und forderst " +
                           "Präzision. Sei direkt. Antworte auf Deutsch.",

        ["enthusiastic"] = "Du bist ein begeisterter Mitschüler! Du findest alles spannend 🎉 " +
                           "und teilst deine Lernfreude. Verwende Ausrufezeichen! " +
                           "Antworte auf Deutsch.",

        ["socratic"]     = "Du bist ein sokratischer Lehrer. Statt direkte Antworten zu geben, " +
                           "stellst du geschickte Gegenfragen, die den Lernenden zum " +
                           "Selbstdenken anregen. Antworte auf Deutsch.",

        ["genius"]       = "Du bist ein hochintelligenter Mentor mit außergewöhnlichem " +
                           "Verständnis. Du siehst Zusammenhänge, die andere übersehen, gibst " +
                           "tiefe Einblicke und erklärst Komplexes elegant. Antworte auf Deutsch.",

        ["humorous"]     = "Du bist ein humorvoller Coach. Nutze Witze, Analogien und lockere " +
                           "Sprache um Konzepte unterhaltsam zu erklären. " +
                           "Lernen soll Spaß machen! Antworte auf Deutsch.",

        ["annoyed"]      = "Du bist ein genervter Professor, der Studenten für frustrierend hält. " +
                           "Du seufzt oft und machst sarkastische Bemerkungen — gibst aber " +
                           "immer vollständig korrekte Antworten. Antworte auf Deutsch.",
    };

    // ── Constructor ───────────────────────────────────────────────────────────

    public ChatViewModel(MindForgeDbContext db, IChatService chat, AISelector ai)
    {
        _db   = db;
        _chat = chat;
        _ai   = ai;

        // DispatcherTimer fires on the UI thread → no Dispatcher.Invoke needed
        _elapsedTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _elapsedTimer.Tick += (_, _) =>
            ElapsedTime = $"{_stopwatch.Elapsed.TotalSeconds:F1}s";
    }

    // ── Initialization ────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        // Load chat history
        var history = await _chat.GetHistoryAsync(UserSession.UserId, 50);
        Messages = new ObservableCollection<ChatMessage>(history);

        // Load materials for context selection (up to 30)
        var materials = await _db.Materials
            .Where(m => m.UserId == UserSession.UserId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(30)
            .ToListAsync();
        AvailableMaterials = new ObservableCollection<Material>(materials);

        // Load Ollama model list (non-blocking — stays empty if Ollama is offline)
        try
        {
            var models = await _ai.GetAvailableModelsAsync();
            AvailableModels = new ObservableCollection<string>(models);
            if (AvailableModels.Count > 0 && string.IsNullOrEmpty(SelectedModel))
                SelectedModel = AvailableModels[0];
        }
        catch { /* Ollama offline — AvailableModels stays empty */ }
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        var prompt = InputText.Trim();
        if (string.IsNullOrWhiteSpace(prompt) || IsBusy) return;

        InputText         = string.Empty;
        IsBusy            = true;
        IsStreaming       = true;
        StreamingResponse = string.Empty;

        // Reset per-response stats
        _sessionTokens = 0;
        TokenDisplay   = "0 tokens";
        ElapsedTime    = "0.0s";
        _stopwatch.Restart();
        _elapsedTimer.Start();

        var context      = BuildContext();
        var systemPrompt = BuildSystemPrompt();
        var modelOverride = string.IsNullOrEmpty(SelectedModel) ? null : SelectedModel;
        _ai.SetTemperature(Temperature);
        _cts = new CancellationTokenSource();
        var sb = new StringBuilder();

        try
        {
            // Add user bubble immediately
            Messages.Add(new ChatMessage
            {
                Id        = Guid.NewGuid(),
                UserId    = UserSession.UserId,
                Content   = prompt,
                Role      = ChatRole.User,
                CreatedAt = DateTime.UtcNow
            });

            await foreach (var chunk in _chat.StreamMessageAsync(
                UserSession.UserId, prompt, context, systemPrompt, modelOverride, _cts.Token))
            {
                sb.Append(chunk);
                StreamingResponse = sb.ToString();

                // Live token estimate: 1 token ≈ 4 chars
                _sessionTokens += Math.Max(1, chunk.Length / 4);
                TokenDisplay    = $"{_sessionTokens} tokens";
            }

            if (sb.Length > 0)
            {
                Messages.Add(new ChatMessage
                {
                    Id        = Guid.NewGuid(),
                    UserId    = UserSession.UserId,
                    Content   = sb.ToString(),
                    Role      = ChatRole.Assistant,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Id        = Guid.NewGuid(),
                UserId    = UserSession.UserId,
                Content   = $"⚠️ Fehler: {ex.Message}",
                Role      = ChatRole.Assistant,
                CreatedAt = DateTime.UtcNow
            });
        }
        finally
        {
            _elapsedTimer.Stop();
            _stopwatch.Stop();
            StreamingResponse = string.Empty;
            IsStreaming       = false;
            IsBusy            = false;
        }
    }

    [RelayCommand] public void StopGeneration() => _cts?.Cancel();

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        await _chat.ClearHistoryAsync(UserSession.UserId);
        Messages.Clear();
        _sessionTokens = 0;
        TokenDisplay   = "0 tokens";
    }

    // ── Context material toggling ─────────────────────────────────────────────

    public void ToggleContextMaterial(Material material)
    {
        if (ContextMaterials.Contains(material))
            ContextMaterials.Remove(material);
        else
            ContextMaterials.Add(material);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string BuildContext()
    {
        if (ContextMaterials.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        foreach (var m in ContextMaterials)
        {
            var excerpt = m.KiContent.Length > 2000
                ? m.KiContent[..2000] + "…"
                : m.KiContent;
            sb.AppendLine($"[{m.OriginalFileName}]");
            sb.AppendLine(excerpt);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private string BuildSystemPrompt()
    {
        var key = SelectedPersonalityIndex >= 0 && SelectedPersonalityIndex < _personalityKeys.Length
            ? _personalityKeys[SelectedPersonalityIndex]
            : "neutral";

        var sb = new StringBuilder(_personalityPrompts[key]);

        // Custom prompt override appended after personality
        if (!string.IsNullOrWhiteSpace(CustomPrompt))
        {
            sb.AppendLine();
            sb.AppendLine(CustomPrompt.Trim());
        }

        // Response-style hints
        var hints = new List<string>();
        if (UseExamples)    hints.Add("Nutze konkrete Beispiele.");
        if (UseStepByStep)  hints.Add("Erkläre Schritt-für-Schritt.");
        if (UseCitations)   hints.Add("Zitiere relevante Quellen wenn möglich.");
        if (hints.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(string.Join(" ", hints));
        }

        return sb.ToString();
    }
}
