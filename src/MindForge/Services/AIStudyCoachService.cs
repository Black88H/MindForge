using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MindForge.Data;
using MindForge.Models;
using MindForge.Services.AI;
using MindForge.Services.Interfaces;

namespace MindForge.Services;

public class AIStudyCoachService : IAIStudyCoachService
{
    private readonly MindForgeDbContext _db;
    private readonly AISelector         _ai;

    public AIStudyCoachService(MindForgeDbContext db, AISelector ai)
    {
        _db = db;
        _ai = ai;
    }

    public async Task<CoachReport> GetRecommendationsAsync(Guid userId)
    {
        var today  = DateTime.UtcNow.Date;
        var cutoff = today.AddDays(-14);

        var stats = await _db.StudyStatistics
            .Where(s => s.UserId == userId && s.Date >= cutoff)
            .OrderBy(s => s.Date)
            .ToListAsync();

        var sessions = await _db.StudySessions
            .Where(s => s.UserId == userId && s.StartedAt >= cutoff)
            .ToListAsync();

        var items = await _db.SpacedRepetitionItems
            .Where(i => i.UserId == userId)
            .ToListAsync();

        int daysStudied     = stats.Count(s => s.MinutesStudied > 0);
        int avgMinutes      = stats.Any() ? (int)stats.Average(s => s.MinutesStudied) : 0;
        int recentMinutes   = stats.TakeLast(7).Sum(s => s.MinutesStudied);
        int prevMinutes     = stats.Take(7).Sum(s => s.MinutesStudied);
        double avgScore     = stats.Where(s => s.TestsTaken > 0).Any()
            ? stats.Where(s => s.TestsTaken > 0).Average(s => s.AverageScore) : 0;
        int dueItems        = items.Count(i => i.NextReviewDate <= DateTime.UtcNow);
        int totalItems      = items.Count;

        var prompt =
            "You are an expert AI study coach. Analyze this learner's data and provide personalized recommendations.\n\n" +
            "LEARNER DATA (last 14 days):\n" +
            $"- Days studied: {daysStudied}/14\n" +
            $"- Average study minutes/day: {avgMinutes}\n" +
            $"- Recent 7-day total minutes: {recentMinutes}\n" +
            $"- Previous 7-day total minutes: {prevMinutes}\n" +
            $"- Average test score: {avgScore:F1}%\n" +
            $"- Flashcards due for review: {dueItems}/{totalItems}\n" +
            $"- Total study sessions: {sessions.Count}\n\n" +
            "Respond ONLY with a JSON object in this exact format (no markdown, no extra text):\n" +
            "{\n" +
            "  \"recommendations\": [\n" +
            "    {\n" +
            "      \"title\": \"...\",\n" +
            "      \"description\": \"...\",\n" +
            "      \"priority\": \"high\",\n" +
            "      \"category\": \"study-technique\",\n" +
            "      \"actionSteps\": [\"step1\", \"step2\"]\n" +
            "    }\n" +
            "  ],\n" +
            "  \"weeklyGoal\": \"...\",\n" +
            "  \"encouragement\": \"...\",\n" +
            "  \"burnoutRisk\": \"low\",\n" +
            "  \"recommendedDailyMinutes\": 45\n" +
            "}";

        try
        {
            var (provider, model) = await _ai.SelectAsync(AITask.StudyGuide);
            var json = await provider.GenerateAsync(model, prompt);

            var start = json.IndexOf('{');
            var end   = json.LastIndexOf('}');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var recs = new List<CoachRecommendation>();
            if (root.TryGetProperty("recommendations", out var recsEl))
            {
                foreach (var r in recsEl.EnumerateArray())
                {
                    var steps = new List<string>();
                    if (r.TryGetProperty("actionSteps", out var stepsEl))
                        foreach (var s in stepsEl.EnumerateArray())
                            steps.Add(s.GetString() ?? "");

                    recs.Add(new CoachRecommendation(
                        r.TryGetProperty("title",       out var t)    ? t.GetString()    ?? "" : "",
                        r.TryGetProperty("description", out var d)    ? d.GetString()    ?? "" : "",
                        r.TryGetProperty("priority",    out var p)    ? p.GetString()    ?? "medium" : "medium",
                        r.TryGetProperty("category",    out var cat)  ? cat.GetString()  ?? "study-technique" : "study-technique",
                        steps));
                }
            }

            return new CoachReport(
                recs.Count > 0 ? recs : BuildFallbackRecs(daysStudied, dueItems),
                root.TryGetProperty("weeklyGoal",              out var wg)  ? wg.GetString()  ?? BuildWeeklyGoal(avgMinutes) : BuildWeeklyGoal(avgMinutes),
                root.TryGetProperty("encouragement",           out var enc) ? enc.GetString() ?? BuildEncouragement(daysStudied) : BuildEncouragement(daysStudied),
                root.TryGetProperty("burnoutRisk",             out var br)  ? br.GetString()  ?? CalcBurnout(recentMinutes, prevMinutes) : CalcBurnout(recentMinutes, prevMinutes),
                root.TryGetProperty("recommendedDailyMinutes", out var rdm) ? rdm.GetInt32()  : Math.Max(30, avgMinutes + 10));
        }
        catch
        {
            return new CoachReport(
                BuildFallbackRecs(daysStudied, dueItems),
                BuildWeeklyGoal(avgMinutes),
                BuildEncouragement(daysStudied),
                CalcBurnout(recentMinutes, prevMinutes),
                Math.Max(30, avgMinutes + 10));
        }
    }

    public async Task<string> GetQuickTipAsync(Guid userId)
    {
        var tips = new[]
        {
            "Nutze die Pomodoro-Technik: 25 Minuten Fokus, dann 5 Minuten Pause.",
            "Wiederhole Flashcards kurz vor dem Schlafen - das verbessert die Konsolidierung im Langzeitgedaechtnis.",
            "Erklaere das Gelernte einer anderen Person - der Feynman-Trick ist hocheffektiv.",
            "Wechsle regelmaessig das Thema - abwechselndes Lernen verbessert die Retention.",
            "Trinke ausreichend Wasser - Dehydrierung reduziert die kognitive Leistung um bis zu 10%.",
            "Starte mit den schwersten Aufgaben, wenn dein Energielevel am hoechsten ist.",
            "Erstelle Mind-Maps, um Zusammenhaenge zu visualisieren.",
            "Nutze aktives Erinnern statt passivem Lesen - teste dich selbst nach jedem Abschnitt."
        };

        try
        {
            var today = DateTime.UtcNow.Date;
            var stat  = await _db.StudyStatistics
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Date == today);

            var prompt = "Give one short, specific, actionable study tip in German (1-2 sentences). No formatting, just the tip text.";

            if (stat?.MinutesStudied > 120)
                prompt = "The student has studied more than 2 hours today. Give a tip about rest and recovery in German (1-2 sentences).";
            else if (stat?.MinutesStudied == 0)
                prompt = "The student has not studied yet today. Give a motivating tip to get started in German (1-2 sentences).";

            var (provider, model) = await _ai.SelectAsync(AITask.Chat);
            return await provider.GenerateAsync(model, prompt);
        }
        catch
        {
            return tips[DateTime.UtcNow.DayOfYear % tips.Length];
        }
    }

    // ── Fallback helpers ──────────────────────────────────────────────────────

    private static List<CoachRecommendation> BuildFallbackRecs(int daysStudied, int dueItems)
    {
        var recs = new List<CoachRecommendation>();

        if (daysStudied < 5)
            recs.Add(new CoachRecommendation(
                "Lernkontinuitaet aufbauen",
                "Du hast in den letzten 14 Tagen weniger als 5 Tage gelernt. Regelmaeßigkeit ist der Schluessel zum Erfolg.",
                "high", "time-management",
                ["Setze dir einen festen Lernblock jeden Tag", "Beginne mit nur 20 Minuten um die Gewohnheit zu etablieren"]));

        if (dueItems > 10)
            recs.Add(new CoachRecommendation(
                "Flashcards aufholen",
                $"Du hast {dueItems} Flashcards, die auf ihre Wiederholung warten. Spaced Repetition funktioniert nur mit regelmaessiger Übung.",
                "high", "content-focus",
                ["Reviewe heute mindestens 20 Flashcards", "Plane taeglich 10 Minuten fuer Flashcard-Review ein"]));

        recs.Add(new CoachRecommendation(
            "Aktives Erinnern praktizieren",
            "Statt passiv zu lesen, teste dich selbst mit Fragen und Lückentext.",
            "medium", "study-technique",
            ["Schliesse das Buch und schreibe auf, was du weisst", "Nutze den adaptiven Quiz nach jeder Lerneinheit"]));

        return recs;
    }

    private static string BuildWeeklyGoal(int avgMinutes)
        => avgMinutes < 30
            ? "Erreiche diese Woche 5 Tage mit mindestens 30 Minuten Lernzeit."
            : $"Steigere deine Lernzeit auf {avgMinutes + 15} Minuten pro Tag und behalte eine 5-Tage-Streak.";

    private static string BuildEncouragement(int daysStudied)
        => daysStudied >= 10
            ? "Hervorragend! Deine Konsistenz ist beeindruckend. Bleib dran!"
            : daysStudied >= 5
                ? "Gute Arbeit! Du bist auf dem richtigen Weg. Jeder Tag zaehlt."
                : "Jede Reise beginnt mit einem Schritt. Starte heute und baue deinen Rhythmus auf!";

    private static string CalcBurnout(int recentMinutes, int prevMinutes)
    {
        if (prevMinutes == 0) return "low";
        var ratio = (double)recentMinutes / prevMinutes;
        return ratio > 1.5 ? "high" : ratio > 1.2 ? "medium" : "low";
    }
}
