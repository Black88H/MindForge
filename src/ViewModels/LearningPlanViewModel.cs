using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MindForge.Models;
using MindForge.Services;
using MindForge.Utils;

namespace MindForge.ViewModels;

public partial class LearningPlanViewModel : ObservableObject
{
    private readonly ILearningPlanService _planService;
    private readonly ISpacedRepetitionService _srService;

    // ── View state ────────────────────────────────────────────────────────────
    [ObservableProperty] private string _viewMode = "List";          // "List" | "Calendar"
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private bool _isGenerating = false;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Today's tasks ─────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<LearningTask> _todaysTasks = new();
    public bool HasTasks => TodaysTasks.Count > 0;
    [ObservableProperty] private int _completedCount = 0;
    [ObservableProperty] private int _totalCount = 0;

    public string ProgressText => TotalCount == 0
        ? "Keine Aufgaben heute"
        : $"{CompletedCount} / {TotalCount} erledigt";
    public double ProgressRatio => TotalCount == 0 ? 0 : (double)CompletedCount / TotalCount;

    // ── Calendar ──────────────────────────────────────────────────────────────
    [ObservableProperty] private int _calendarYear  = DateTime.Now.Year;
    [ObservableProperty] private int _calendarMonth = DateTime.Now.Month;
    [ObservableProperty] private ObservableCollection<CalendarDay> _calendarDays = new();
    public string CalendarTitle => new DateTime(CalendarYear, CalendarMonth, 1).ToString("MMMM yyyy");

    // ── Plan generation ───────────────────────────────────────────────────────
    [ObservableProperty] private DateTime? _goalDate;
    [ObservableProperty] private string _selectedSubjectName = string.Empty;
    private Guid _selectedSubjectId = Guid.Empty;

    public LearningPlanViewModel(ILearningPlanService planService, ISpacedRepetitionService srService)
    {
        _planService = planService;
        _srService   = srService;
    }

    public async Task InitializeAsync(Guid subjectId, string subjectName)
    {
        _selectedSubjectId  = subjectId;
        SelectedSubjectName = subjectName;
        await LoadTodaysTasksAsync();
    }

    // ── Computed notifications ────────────────────────────────────────────────
    partial void OnCompletedCountChanged(int value)
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressRatio));
    }
    partial void OnTotalCountChanged(int value)
    {
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressRatio));
    }
    partial void OnCalendarYearChanged(int value)  => OnPropertyChanged(nameof(CalendarTitle));
    partial void OnCalendarMonthChanged(int value) => OnPropertyChanged(nameof(CalendarTitle));

    // ── Commands ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task GeneratePlanAsync()
    {
        if (_selectedSubjectId == Guid.Empty)
        {
            StatusMessage = "Bitte zuerst ein Fach wählen.";
            return;
        }
        IsGenerating = true;
        StatusMessage = string.Empty;
        try
        {
            await _planService.GeneratePlanAsync(_selectedSubjectId, GoalDate, UserSession.UserId);
            StatusMessage = "✓ Lernplan erstellt";
            await LoadTodaysTasksAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    [RelayCommand]
    private async Task CompleteTaskAsync(LearningTask task)
    {
        if (task.CompletedAt != null) return;
        try
        {
            await _planService.CompleteTaskAsync(task.Id, UserSession.UserId);
            task.CompletedAt = DateTime.UtcNow;
            CompletedCount++;
            // Force CollectionChanged so the checkbox updates
            var idx = TodaysTasks.IndexOf(task);
            if (idx >= 0)
            {
                TodaysTasks.RemoveAt(idx);
                TodaysTasks.Insert(idx, task);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
        }
    }

    [RelayCommand]
    private void SwitchToList()    => ViewMode = "List";

    [RelayCommand]
    private void SwitchToCalendar()
    {
        ViewMode = "Calendar";
        _ = LoadCalendarAsync();
    }

    [RelayCommand]
    private async Task PreviousMonthAsync()
    {
        if (CalendarMonth == 1) { CalendarMonth = 12; CalendarYear--; }
        else                    { CalendarMonth--; }
        await LoadCalendarAsync();
    }

    [RelayCommand]
    private async Task NextMonthAsync()
    {
        if (CalendarMonth == 12) { CalendarMonth = 1; CalendarYear++; }
        else                     { CalendarMonth++; }
        await LoadCalendarAsync();
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private async Task LoadTodaysTasksAsync()
    {
        IsLoading = true;
        try
        {
            var tasks = await _planService.GetTodaysTasksAsync(UserSession.UserId);
            TodaysTasks.Clear();
            foreach (var t in tasks) TodaysTasks.Add(t);
            TotalCount     = tasks.Count;
            CompletedCount = tasks.Count(t => t.CompletedAt != null);
        }
        finally { IsLoading = false; }
    }

    private async Task LoadCalendarAsync()
    {
        IsLoading = true;
        try
        {
            var map = await _planService.GetCalendarViewAsync(UserSession.UserId, CalendarYear, CalendarMonth);
            CalendarDays.Clear();
            var firstDay = new DateTime(CalendarYear, CalendarMonth, 1);
            int blanks   = ((int)firstDay.DayOfWeek + 6) % 7;  // Mo=0

            for (int i = 0; i < blanks; i++)
                CalendarDays.Add(new CalendarDay { IsBlank = true });

            int daysInMonth = DateTime.DaysInMonth(CalendarYear, CalendarMonth);
            for (int day = 1; day <= daysInMonth; day++)
            {
                var date = new DateTime(CalendarYear, CalendarMonth, day);
                map.TryGetValue(date, out var dayTasks);
                CalendarDays.Add(new CalendarDay
                {
                    Date      = date,
                    DayNumber = day,
                    IsToday   = date.Date == DateTime.Today,
                    Tasks     = dayTasks ?? new List<LearningTask>()
                });
            }
        }
        finally { IsLoading = false; }
    }
}

/// <summary>Represents one cell in the calendar grid.</summary>
public class CalendarDay
{
    public bool             IsBlank   { get; set; }
    public DateTime         Date      { get; set; }
    public int              DayNumber { get; set; }
    public bool             IsToday   { get; set; }
    public List<LearningTask> Tasks   { get; set; } = new();
    public bool             HasTasks  => Tasks.Count > 0;
    public string           TaskCount => Tasks.Count == 1 ? "1 Aufgabe" : $"{Tasks.Count} Aufgaben";
}
