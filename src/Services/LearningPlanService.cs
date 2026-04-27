using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MindForge.Models;

namespace MindForge.Services;

public interface ILearningPlanService
{
    Task<LearningPlan> GeneratePlanAsync(Guid subjectId, DateTime? goalDate, Guid userId);
    Task<List<LearningTask>> GetTodaysTasksAsync(Guid userId);
    Task CompleteTaskAsync(Guid taskId, Guid userId);
    Task<Dictionary<DateTime, List<LearningTask>>> GetCalendarViewAsync(Guid userId, int year, int month);
}

public class LearningPlanService : ILearningPlanService
{
    private readonly MindForgeDbContext _db;
    private readonly IKnowledgeGraphService _graph;
    private readonly ISpacedRepetitionService _sr;
    private readonly IGamificationService _gamification;
    private readonly ILogger<LearningPlanService> _logger;

    public LearningPlanService(
        MindForgeDbContext db, IKnowledgeGraphService graph,
        ISpacedRepetitionService sr, IGamificationService gamification,
        ILogger<LearningPlanService> logger)
    {
        _db = db;
        _graph = graph;
        _sr = sr;
        _gamification = gamification;
        _logger = logger;
    }

    public async Task<LearningPlan> GeneratePlanAsync(Guid subjectId, DateTime? goalDate, Guid userId)
    {
        var nodes = await _graph.GetNodesForSubjectAsync(subjectId, userId);
        if (!nodes.Any())
            throw new InvalidOperationException("Keine Wissensknoten vorhanden. Lade zuerst Material hoch.");

        // Alte aktive Pläne deaktivieren
        var oldPlans = await _db.Set<LearningPlan>()
            .Where(p => p.UserId == userId && p.SubjectId == subjectId && p.IsActive)
            .ToListAsync();
        foreach (var old in oldPlans) old.IsActive = false;

        var plan = new LearningPlan
        {
            UserId = userId,
            SubjectId = subjectId,
            Title = $"Lernplan — {(await _db.Set<Subject>().FindAsync(subjectId))?.Name ?? "Fach"}",
            GoalDate = goalDate,
            IsActive = true
        };
        _db.Set<LearningPlan>().Add(plan);

        // Aufgaben generieren
        var weakNodes = nodes.Where(n => n.MasteryLevel < 0.8f).OrderBy(n => n.MasteryLevel).ToList();
        var strongNodes = nodes.Where(n => n.MasteryLevel >= 0.8f).ToList();

        var today = DateTime.UtcNow.Date;
        int daysAvailable = goalDate.HasValue ? Math.Max(1, (goalDate.Value.Date - today).Days) : 30;
        int tasksPerDay = Math.Max(1, (weakNodes.Count + strongNodes.Count / 3) / daysAvailable);

        int dayOffset = 0;
        int taskIndex = 0;

        // Schwache Nodes zuerst
        foreach (var node in weakNodes)
        {
            var scheduleDate = today.AddDays(dayOffset);
            _db.LearningTasks.Add(new LearningTask
            {
                PlanId = plan.Id,
                KnowledgeNodeId = node.Id,
                Title = $"Lernen: {node.Title}",
                Description = node.Summary,
                ScheduledDate = scheduleDate,
                TaskType = LearningTaskType.NewContent,
                DurationMinutes = 20,
                Priority = node.MasteryLevel < 0.3f ? 1 : 2
            });

            // Spaced Repetition schedulen
            await _sr.ScheduleReviewAsync(userId, node.Id);

            taskIndex++;
            if (taskIndex >= tasksPerDay)
            {
                taskIndex = 0;
                dayOffset++;
            }
        }

        // Wiederholungen für starke Nodes
        foreach (var node in strongNodes)
        {
            var reviewDate = today.AddDays(dayOffset + 3); // Etwas später
            _db.LearningTasks.Add(new LearningTask
            {
                PlanId = plan.Id,
                KnowledgeNodeId = node.Id,
                Title = $"Wiederholen: {node.Title}",
                Description = node.Summary,
                ScheduledDate = reviewDate,
                TaskType = LearningTaskType.Review,
                DurationMinutes = 10,
                Priority = 4
            });

            taskIndex++;
            if (taskIndex >= tasksPerDay)
            {
                taskIndex = 0;
                dayOffset++;
            }
        }

        // Feynman-Checks einstreuen
        if (weakNodes.Count >= 3)
        {
            var feynmanDate = today.AddDays(Math.Max(3, daysAvailable / 2));
            _db.LearningTasks.Add(new LearningTask
            {
                PlanId = plan.Id,
                Title = "Feynman-Check: Erkläre deine Schwachstellen",
                Description = "Erkläre die schwierigsten Konzepte wie einem 5-Jährigen",
                ScheduledDate = feynmanDate,
                TaskType = LearningTaskType.FeynmanCheck,
                DurationMinutes = 15,
                Priority = 2
            });
        }

        // Test am Ende
        var testDate = goalDate?.AddDays(-2) ?? today.AddDays(daysAvailable - 1);
        _db.LearningTasks.Add(new LearningTask
        {
            PlanId = plan.Id,
            Title = "Abschlusstest",
            Description = "Teste dein Wissen mit einem umfassenden Test",
            ScheduledDate = testDate,
            TaskType = LearningTaskType.Test,
            DurationMinutes = 30,
            Priority = 1
        });

        await _db.SaveChangesAsync();
        return plan;
    }

    public async Task<List<LearningTask>> GetTodaysTasksAsync(Guid userId)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.LearningTasks
            .Include(t => t.Plan)
            .Where(t => t.Plan!.UserId == userId && t.Plan.IsActive
                && t.ScheduledDate.Date <= today && t.CompletedAt == null)
            .OrderBy(t => t.Priority)
            .ThenBy(t => t.ScheduledDate)
            .ToListAsync();
    }

    public async Task CompleteTaskAsync(Guid taskId, Guid userId)
    {
        var task = await _db.LearningTasks.FindAsync(taskId)
            ?? throw new ArgumentException("Aufgabe nicht gefunden");

        task.CompletedAt = DateTime.UtcNow;
        await _gamification.AwardXPAsync(userId, 15, XPSource.LessonCompleted, $"Aufgabe erledigt: {task.Title}");
        await _gamification.UpdateStreakAsync(userId);
        await _db.SaveChangesAsync();
    }

    public async Task<Dictionary<DateTime, List<LearningTask>>> GetCalendarViewAsync(Guid userId, int year, int month)
    {
        var startDate = new DateTime(year, month, 1);
        var endDate = startDate.AddMonths(1);

        var tasks = await _db.LearningTasks
            .Include(t => t.Plan)
            .Where(t => t.Plan!.UserId == userId && t.Plan.IsActive
                && t.ScheduledDate >= startDate && t.ScheduledDate < endDate)
            .OrderBy(t => t.ScheduledDate)
            .ToListAsync();

        return tasks.GroupBy(t => t.ScheduledDate.Date)
            .ToDictionary(g => g.Key, g => g.ToList());
    }
}
