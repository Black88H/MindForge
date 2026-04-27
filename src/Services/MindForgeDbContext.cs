using System.IO;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class MindForgeDbContext : DbContext
{
    public MindForgeDbContext(DbContextOptions<MindForgeDbContext> options) : base(options) { }

    // Auth
    public DbSet<User>        Users        => Set<User>();

    // Existing
    public DbSet<Question>    Questions    => Set<Question>();
    public DbSet<Answer>      Answers      => Set<Answer>();
    public DbSet<Subject>     Subjects     => Set<Subject>();
    public DbSet<Test>        Tests        => Set<Test>();
    public DbSet<TestResult>  TestResults  => Set<TestResult>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<TokenUsage>  TokenUsage   => Set<TokenUsage>();

    // New v1.0.0
    public DbSet<LearningPlan>          LearningPlans          => Set<LearningPlan>();
    public DbSet<LearningMethod>        LearningMethods        => Set<LearningMethod>();
    public DbSet<LearningPlanMethod>    LearningPlanMethods    => Set<LearningPlanMethod>();
    public DbSet<UserLearningProfile>   UserLearningProfiles   => Set<UserLearningProfile>();
    public DbSet<UserTestHistory>       UserTestHistory        => Set<UserTestHistory>();
    public DbSet<MaterialLibrary>       MaterialLibrary        => Set<MaterialLibrary>();
    public DbSet<OCRDocument>           OCRDocuments           => Set<OCRDocument>();
    public DbSet<SpacedRepetitionItem>  SpacedRepetitionItems  => Set<SpacedRepetitionItem>();
    public DbSet<Challenge>             Challenges             => Set<Challenge>();
    public DbSet<UserChallenge>         UserChallenges         => Set<UserChallenge>();
    public DbSet<Notification>          Notifications          => Set<Notification>();

    // New MindForge Phase 1
    public DbSet<SystemSetting>         SystemSettings         => Set<SystemSetting>();
    public DbSet<Material>              Materials              => Set<Material>();
    public DbSet<KnowledgeNode>         KnowledgeNodes         => Set<KnowledgeNode>();
    public DbSet<KnowledgeEdge>         KnowledgeEdges         => Set<KnowledgeEdge>();
    public DbSet<LearningTask>          LearningTasks          => Set<LearningTask>();
    public DbSet<ChatMessage>           ChatMessages           => Set<ChatMessage>();
    public DbSet<TestQuestion>          TestQuestions          => Set<TestQuestion>();
    public DbSet<FeynmanSession>        FeynmanSessions        => Set<FeynmanSession>();
    public DbSet<XPEvent>               XPEvents               => Set<XPEvent>();
    public DbSet<Badge>                 Badges                 => Set<Badge>();
    public DbSet<UserBadge>             UserBadges             => Set<UserBadge>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Existing relationships
        mb.Entity<Question>()
          .HasOne(q => q.Subject)
          .WithMany(s => s.Questions)
          .HasForeignKey(q => q.SubjectId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Answer>()
          .HasOne(a => a.Question)
          .WithMany(q => q.Answers)
          .HasForeignKey(a => a.QuestionId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Test>()
          .HasOne(t => t.Subject)
          .WithMany(s => s.Tests)
          .HasForeignKey(t => t.SubjectId)
          .OnDelete(DeleteBehavior.SetNull)
          .IsRequired(false);

        mb.Entity<TestResult>()
          .HasOne(r => r.Test)
          .WithMany(t => t.Results)
          .HasForeignKey(r => r.TestId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<UserProgress>()
          .HasOne(p => p.Subject)
          .WithMany()
          .HasForeignKey(p => p.SubjectId)
          .OnDelete(DeleteBehavior.SetNull)
          .IsRequired(false);

        // New v1.0.0 relationships
        mb.Entity<LearningPlan>()
          .HasOne(lp => lp.Subject)
          .WithMany()
          .HasForeignKey(lp => lp.SubjectId)
          .OnDelete(DeleteBehavior.SetNull)
          .IsRequired(false);

        mb.Entity<LearningPlanMethod>()
          .HasOne(lpm => lpm.LearningPlan)
          .WithMany(lp => lp.Methods)
          .HasForeignKey(lpm => lpm.LearningPlanId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<LearningPlanMethod>()
          .HasOne(lpm => lpm.LearningMethod)
          .WithMany(lm => lm.PlanMethods)
          .HasForeignKey(lpm => lpm.LearningMethodId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<UserTestHistory>()
          .HasOne(uth => uth.Test)
          .WithMany()
          .HasForeignKey(uth => uth.TestId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<MaterialLibrary>()
          .HasOne(ml => ml.Subject)
          .WithMany()
          .HasForeignKey(ml => ml.SubjectId)
          .OnDelete(DeleteBehavior.SetNull)
          .IsRequired(false);

        mb.Entity<SpacedRepetitionItem>()
          .HasOne(sri => sri.UserProgress)
          .WithMany()
          .HasForeignKey(sri => sri.UserProgressId)
          .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<UserChallenge>()
          .HasOne(uc => uc.Challenge)
          .WithMany(c => c.UserChallenges)
          .HasForeignKey(uc => uc.ChallengeId)
          .OnDelete(DeleteBehavior.Cascade);

        // User indexes
        mb.Entity<User>().HasIndex(u => u.Username).IsUnique();
        mb.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // Indexes (existing)
        mb.Entity<Question>().HasIndex(q => q.SubjectId);
        mb.Entity<Answer>().HasIndex(a => a.QuestionId);
        mb.Entity<Answer>().HasIndex(a => a.Timestamp);
        mb.Entity<UserProgress>().HasIndex(p => p.UserId);
        mb.Entity<Subject>().HasIndex(s => s.SortOrder);

        // Indexes (new)
        mb.Entity<LearningPlan>().HasIndex(lp => lp.UserId);
        mb.Entity<UserTestHistory>().HasIndex(uth => uth.UserId);
        mb.Entity<MaterialLibrary>().HasIndex(ml => ml.UserId);
        mb.Entity<Notification>().HasIndex(n => new { n.UserId, n.Read });
        mb.Entity<SpacedRepetitionItem>().HasIndex(sri => sri.NextReviewDate);
        mb.Entity<UserChallenge>().HasIndex(uc => uc.UserId);

        // Seed default achievements
        mb.Entity<Achievement>().HasData(
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Erster Schritt",   Icon = "🥾", Rarity = AchievementRarity.Häufig,   TriggerKey = "questions_answered", TriggerValue = 1,    XpReward = 25,   Description = "Erste Frage beantwortet" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "Wochenkrieger",    Icon = "⚔️",  Rarity = AchievementRarity.Häufig,   TriggerKey = "streak_days",        TriggerValue = 7,    XpReward = 100,  Description = "7 Tage Streak erreicht" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Perfekte Zehn",    Icon = "💜",  Rarity = AchievementRarity.Selten,   TriggerKey = "perfect_session",    TriggerValue = 10,   XpReward = 200,  Description = "10 Fragen in Folge richtig" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Name = "Nachteule",        Icon = "🦉",  Rarity = AchievementRarity.Häufig,   TriggerKey = "study_hour",         TriggerValue = 22,   XpReward = 50,   Description = "Nach 22 Uhr gelernt" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000005"), Name = "Marathonläufer",   Icon = "🏃",  Rarity = AchievementRarity.Selten,   TriggerKey = "streak_days",        TriggerValue = 30,   XpReward = 300,  Description = "30 Tage Streak" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000006"), Name = "Meisterstudent",   Icon = "🎓",  Rarity = AchievementRarity.Episch,   TriggerKey = "questions_answered", TriggerValue = 500,  XpReward = 500,  Description = "500 Fragen beantwortet" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000007"), Name = "Unsterblich",      Icon = "🔥",  Rarity = AchievementRarity.Legendär, TriggerKey = "streak_days",        TriggerValue = 100,  XpReward = 1000, Description = "100 Tage Streak" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000008"), Name = "Tausend Fragen",   Icon = "💯",  Rarity = AchievementRarity.Episch,   TriggerKey = "questions_answered", TriggerValue = 1000, XpReward = 750,  Description = "1000 Fragen beantwortet" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000009"), Name = "Schnelldenker",    Icon = "⚡",  Rarity = AchievementRarity.Selten,   TriggerKey = "fast_answer",        TriggerValue = 5,    XpReward = 150,  Description = "Frage in unter 5 Sekunden" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000010"), Name = "Analyse-Ass",      Icon = "📊",  Rarity = AchievementRarity.Selten,   TriggerKey = "analytics_views",    TriggerValue = 10,   XpReward = 100,  Description = "Analytics 10× geöffnet" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000011"), Name = "Planer",           Icon = "📅",  Rarity = AchievementRarity.Häufig,   TriggerKey = "plans_created",      TriggerValue = 1,    XpReward = 50,   Description = "Ersten Lernplan erstellt" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000012"), Name = "OCR-Meister",      Icon = "🔍",  Rarity = AchievementRarity.Selten,   TriggerKey = "ocr_scans",          TriggerValue = 5,    XpReward = 150,  Description = "5 Dokumente gescannt" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000013"), Name = "Challenger",       Icon = "🏆",  Rarity = AchievementRarity.Häufig,   TriggerKey = "challenges_completed",TriggerValue = 1,   XpReward = 75,   Description = "Erste Challenge abgeschlossen" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000014"), Name = "Lernmaschine",     Icon = "🤖",  Rarity = AchievementRarity.Episch,   TriggerKey = "plans_created",      TriggerValue = 10,   XpReward = 400,  Description = "10 Lernpläne erstellt" }
        );

        // Seed default learning methods
        mb.Entity<LearningMethod>().HasData(
            new LearningMethod { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Name = "Active Recall",     Type = LearningMethodType.ActiveRecall,    Icon = "🧠", Description = "Aktives Erinnern ohne Hinweise" },
            new LearningMethod { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Name = "Pomodoro",          Type = LearningMethodType.Pomodoro,         Icon = "🍅", Description = "25 Min lernen, 5 Min Pause" },
            new LearningMethod { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Name = "Spaced Repetition", Type = LearningMethodType.SpacedRepetition, Icon = "🔄", Description = "SM-2 Algorithmus für optimale Wiederholung" },
            new LearningMethod { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Name = "Interleaving",      Type = LearningMethodType.Interleaving,     Icon = "🔀", Description = "Verschiedene Themen abwechseln" },
            new LearningMethod { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Name = "Practice Test",     Type = LearningMethodType.PracticeTest,     Icon = "📝", Description = "Prüfungssimulation und Tests" }
        );

        mb.Entity<KnowledgeEdge>()
            .HasOne(e => e.FromNode)
            .WithMany(n => n.OutgoingEdges)
            .HasForeignKey(e => e.FromNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<KnowledgeEdge>()
            .HasOne(e => e.ToNode)
            .WithMany(n => n.IncomingEdges)
            .HasForeignKey(e => e.ToNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        mb.Entity<Badge>().HasData(
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000001"), Name = "Erster Upload", Description = "Erstes Material hochgeladen", IconKey = "upload", Requirement = "{\"type\":\"materials_uploaded\",\"count\":1}", XPReward = 50 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000002"), Name = "Wissensdurst", Description = "10 Materialien hochgeladen", IconKey = "books", Requirement = "{\"type\":\"materials_uploaded\",\"count\":10}", XPReward = 200 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000003"), Name = "Testpilot", Description = "Ersten Test abgeschlossen", IconKey = "test", Requirement = "{\"type\":\"tests_completed\",\"count\":1}", XPReward = 50 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000004"), Name = "Perfektionist", Description = "100% in einem Test", IconKey = "star", Requirement = "{\"type\":\"perfect_score\",\"count\":1}", XPReward = 200 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000005"), Name = "Feynman-Meister", Description = "5 Feynman-Sessions bestanden", IconKey = "brain", Requirement = "{\"type\":\"feynman_passed\",\"count\":5}", XPReward = 300 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000006"), Name = "Wochenstreak", Description = "7 Tage in Folge gelernt", IconKey = "fire", Requirement = "{\"type\":\"streak\",\"count\":7}", XPReward = 100 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000007"), Name = "Monatsstreak", Description = "30 Tage in Folge gelernt", IconKey = "fire2", Requirement = "{\"type\":\"streak\",\"count\":30}", XPReward = 500 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000008"), Name = "KI-Flüsterer", Description = "50 Chat-Nachrichten gesendet", IconKey = "chat", Requirement = "{\"type\":\"chat_messages\",\"count\":50}", XPReward = 150 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000009"), Name = "Planer", Description = "Ersten Lernplan erstellt", IconKey = "calendar", Requirement = "{\"type\":\"plans_created\",\"count\":1}", XPReward = 50 },
            new Badge { Id = Guid.Parse("b0000001-0000-0000-0000-000000000010"), Name = "Level 10", Description = "Level 10 erreicht", IconKey = "trophy", Requirement = "{\"type\":\"level\",\"count\":10}", XPReward = 1000 }
        );
    }

    public static string GetDbPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir = Path.Combine(appData, "MindForge");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "mindforge.db");
    }
}
