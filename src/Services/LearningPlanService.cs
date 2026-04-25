using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;

namespace MindForge.Services;

public class LearningPlanService
{
    private readonly MindForgeDbContext _db;
    private readonly IAISelector? _ai;

    public LearningPlanService(MindForgeDbContext db, IAISelector? ai = null)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<LearningPlan> CreateLearningPlanAsync(
        string userId, Guid? subjectId, string title,
        int daysAvailable, int minutesPerDay, List<Guid> methodIds)
    {
        var plan = new LearningPlan
        {
            UserId = userId,
            SubjectId = subjectId,
            Title = title,
            DaysAvailable = daysAvailable,
            MinutesPerDay = minutesPerDay,
            Status = LearningPlanStatus.Active,
            PlannedDate = DateTime.UtcNow.AddDays(daysAvailable)
        };
        _db.LearningPlans.Add(plan);
        await _db.SaveChangesAsync();

        int order = 0;
        foreach (var methodId in methodIds)
        {
            _db.LearningPlanMethods.Add(new LearningPlanMethod
            {
                LearningPlanId = plan.Id,
                LearningMethodId = methodId,
                Order = order++,
                DailyMinutes = minutesPerDay / Math.Max(1, methodIds.Count)
            });
        }
        await _db.SaveChangesAsync();
        return plan;
    }

    public async Task<List<LearningMethod>> SuggestMethodsAsync(Guid? subjectId, string? learningStyle)
    {
        var methods = await _db.LearningMethods.ToListAsync();

        // Heuristic suggestions based on learning style
        if (learningStyle == "Visual")
            return methods.Where(m => m.Type != LearningMethodType.PracticeTest).Take(3).ToList();

        if (learningStyle == "Practice")
            return methods.Where(m => m.Type is LearningMethodType.PracticeTest or LearningMethodType.ActiveRecall).ToList();

        return methods.Take(3).ToList();
    }

    public async Task<List<LearningPlan>> GetUserPlansAsync(string userId)
        => await _db.LearningPlans
            .Include(lp => lp.Subject)
            .Include(lp => lp.Methods).ThenInclude(m => m.LearningMethod)
            .Where(lp => lp.UserId == userId && lp.Status == LearningPlanStatus.Active)
            .OrderByDescending(lp => lp.CreatedDate)
            .ToListAsync();

    public async Task UpdatePlanStatusAsync(Guid planId, LearningPlanStatus status)
    {
        var plan = await _db.LearningPlans.FindAsync(planId);
        if (plan != null)
        {
            plan.Status = status;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<UserLearningProfile> UpdateLearningProfileAsync(
        string userId, List<string> preferredMethods, string learningStyle,
        bool shortExplanations, bool needsExamples, bool needsExercises)
    {
        var profile = await _db.UserLearningProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserLearningProfile { UserId = userId };
            _db.UserLearningProfiles.Add(profile);
        }

        profile.PreferredMethods = preferredMethods;
        profile.LearningStyle = learningStyle;
        profile.ShortExplanations = shortExplanations;
        profile.NeedsExamples = needsExamples;
        profile.NeedsExercises = needsExercises;
        profile.UpdatedDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return profile;
    }
}
