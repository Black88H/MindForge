using System;
using System.Threading.Tasks;

namespace MindForge.Services.Interfaces;

public enum TimerPhase { Work, ShortBreak, LongBreak }

public interface IStudyTimerService
{
    // State
    TimerPhase Phase           { get; }
    TimeSpan   Remaining       { get; }
    bool       IsRunning       { get; }
    int        CompletedPomodoros { get; }

    // Configuration
    TimeSpan WorkDuration       { get; set; }
    TimeSpan ShortBreakDuration { get; set; }
    TimeSpan LongBreakDuration  { get; set; }
    int      LongBreakInterval  { get; set; }

    // Events
    event EventHandler<TimerPhase> PhaseChanged;
    event EventHandler             Tick;
    event EventHandler             SessionCompleted;

    void Start();
    void Pause();
    void Reset();
    void Skip();
    Task SaveSessionAsync(System.Guid userId, System.Guid? notebookId = null);
}
