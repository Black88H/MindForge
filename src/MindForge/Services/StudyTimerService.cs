using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class StudyTimerService : IStudyTimerService, IDisposable
{
    // ── Config ─────────────────────────────────────────────────────────────────
    public TimeSpan WorkDuration       { get; set; } = TimeSpan.FromMinutes(25);
    public TimeSpan ShortBreakDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan LongBreakDuration  { get; set; } = TimeSpan.FromMinutes(15);
    public int      LongBreakInterval  { get; set; } = 4;

    // ── State ──────────────────────────────────────────────────────────────────
    public TimerPhase Phase              { get; private set; } = TimerPhase.Work;
    public TimeSpan   Remaining          { get; private set; }
    public bool       IsRunning          { get; private set; }
    public int        CompletedPomodoros { get; private set; }

    // ── Events ─────────────────────────────────────────────────────────────────
    public event EventHandler<TimerPhase>? PhaseChanged;
    public event EventHandler?             Tick;
    public event EventHandler?             SessionCompleted;

    // ── Internals ──────────────────────────────────────────────────────────────
    private readonly IServiceScopeFactory _scopeFactory;
    private System.Timers.Timer?          _timer;
    private DateTime                      _phaseStarted;

    public StudyTimerService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        Remaining     = WorkDuration;
    }

    public void Start()
    {
        if (IsRunning) return;
        IsRunning    = true;
        _phaseStarted = DateTime.UtcNow;

        _timer = new System.Timers.Timer(1000);
        _timer.Elapsed += OnTick;
        _timer.Start();
    }

    public void Pause()
    {
        IsRunning = false;
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    public void Reset()
    {
        Pause();
        Phase     = TimerPhase.Work;
        Remaining = WorkDuration;
        CompletedPomodoros = 0;
        PhaseChanged?.Invoke(this, Phase);
    }

    public void Skip()
    {
        Pause();
        AdvancePhase();
    }

    private void OnTick(object? sender, System.Timers.ElapsedEventArgs e)
    {
        Remaining = Remaining.Subtract(TimeSpan.FromSeconds(1));
        Tick?.Invoke(this, EventArgs.Empty);

        if (Remaining <= TimeSpan.Zero)
            AdvancePhase();
    }

    private void AdvancePhase()
    {
        Pause();

        if (Phase == TimerPhase.Work)
        {
            CompletedPomodoros++;
            SessionCompleted?.Invoke(this, EventArgs.Empty);

            Phase = CompletedPomodoros % LongBreakInterval == 0
                ? TimerPhase.LongBreak
                : TimerPhase.ShortBreak;
        }
        else
        {
            Phase = TimerPhase.Work;
        }

        Remaining = Phase switch
        {
            TimerPhase.Work       => WorkDuration,
            TimerPhase.ShortBreak => ShortBreakDuration,
            TimerPhase.LongBreak  => LongBreakDuration,
            _                     => WorkDuration
        };

        PhaseChanged?.Invoke(this, Phase);
    }

    public async Task SaveSessionAsync(Guid userId, Guid? notebookId = null)
    {
        var minutes = (int)(DateTime.UtcNow - _phaseStarted).TotalMinutes;
        if (minutes < 1) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MindForgeDbContext>();

        db.StudySessions.Add(new StudySession
        {
            Id              = Guid.NewGuid(),
            UserId          = userId,
            NotebookId      = notebookId,
            SessionType     = "Pomodoro",
            StartedAt       = _phaseStarted,
            EndedAt         = DateTime.UtcNow,
            DurationMinutes = minutes,
            Completed       = true,
            XPEarned        = minutes * 2
        });
        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _timer?.Stop();
        _timer?.Dispose();
    }
}
