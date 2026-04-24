using System.IO;
using Microsoft.EntityFrameworkCore;
using MindForge.Models;

namespace MindForge.Services;

public class MindForgeDbContext : DbContext
{
    public MindForgeDbContext(DbContextOptions<MindForgeDbContext> options) : base(options) { }

    public DbSet<Question>    Questions    => Set<Question>();
    public DbSet<Answer>      Answers      => Set<Answer>();
    public DbSet<Subject>     Subjects     => Set<Subject>();
    public DbSet<Test>        Tests        => Set<Test>();
    public DbSet<TestResult>  TestResults  => Set<TestResult>();
    public DbSet<UserProgress> UserProgress => Set<UserProgress>();
    public DbSet<Achievement> Achievements => Set<Achievement>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Question → Subject (FK)
        mb.Entity<Question>()
          .HasOne(q => q.Subject)
          .WithMany(s => s.Questions)
          .HasForeignKey(q => q.SubjectId)
          .OnDelete(DeleteBehavior.Cascade);

        // Answer → Question (FK)
        mb.Entity<Answer>()
          .HasOne(a => a.Question)
          .WithMany(q => q.Answers)
          .HasForeignKey(a => a.QuestionId)
          .OnDelete(DeleteBehavior.Cascade);

        // Test → Subject (optional FK)
        mb.Entity<Test>()
          .HasOne(t => t.Subject)
          .WithMany(s => s.Tests)
          .HasForeignKey(t => t.SubjectId)
          .OnDelete(DeleteBehavior.SetNull)
          .IsRequired(false);

        // TestResult → Test
        mb.Entity<TestResult>()
          .HasOne(r => r.Test)
          .WithMany(t => t.Results)
          .HasForeignKey(r => r.TestId)
          .OnDelete(DeleteBehavior.Cascade);

        // UserProgress → Subject (optional)
        mb.Entity<UserProgress>()
          .HasOne(p => p.Subject)
          .WithMany()
          .HasForeignKey(p => p.SubjectId)
          .OnDelete(DeleteBehavior.SetNull)
          .IsRequired(false);

        // Indexes
        mb.Entity<Question>().HasIndex(q => q.SubjectId);
        mb.Entity<Answer>().HasIndex(a => a.QuestionId);
        mb.Entity<Answer>().HasIndex(a => a.Timestamp);
        mb.Entity<UserProgress>().HasIndex(p => p.UserId);
        mb.Entity<Subject>().HasIndex(s => s.SortOrder);

        // Seed default achievements
        mb.Entity<Achievement>().HasData(
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Erster Schritt",   Icon = "🥾", Rarity = AchievementRarity.Häufig,   TriggerKey = "questions_answered", TriggerValue = 1,    XpReward = 25,  Description = "Erste Frage beantwortet" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "Wochenkrieger",    Icon = "⚔️",  Rarity = AchievementRarity.Häufig,   TriggerKey = "streak_days",        TriggerValue = 7,    XpReward = 100, Description = "7 Tage Streak erreicht" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Perfekte Zehn",    Icon = "💜",  Rarity = AchievementRarity.Selten,   TriggerKey = "perfect_session",    TriggerValue = 10,   XpReward = 200, Description = "10 Fragen in Folge richtig" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000004"), Name = "Nachteule",        Icon = "🦉",  Rarity = AchievementRarity.Häufig,   TriggerKey = "study_hour",         TriggerValue = 22,   XpReward = 50,  Description = "Nach 22 Uhr gelernt" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000005"), Name = "Marathonläufer",   Icon = "🏃",  Rarity = AchievementRarity.Selten,   TriggerKey = "streak_days",        TriggerValue = 30,   XpReward = 300, Description = "30 Tage Streak" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000006"), Name = "Meisterstudent",   Icon = "🎓",  Rarity = AchievementRarity.Episch,   TriggerKey = "questions_answered", TriggerValue = 500,  XpReward = 500, Description = "500 Fragen beantwortet" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000007"), Name = "Unsterblich",      Icon = "🔥",  Rarity = AchievementRarity.Legendär, TriggerKey = "streak_days",        TriggerValue = 100,  XpReward = 1000,Description = "100 Tage Streak" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000008"), Name = "Tausend Fragen",   Icon = "💯",  Rarity = AchievementRarity.Episch,   TriggerKey = "questions_answered", TriggerValue = 1000, XpReward = 750, Description = "1000 Fragen beantwortet" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000009"), Name = "Schnelldenker",    Icon = "⚡",  Rarity = AchievementRarity.Selten,   TriggerKey = "fast_answer",        TriggerValue = 5,    XpReward = 150, Description = "Frage in unter 5 Sekunden" },
            new Achievement { Id = Guid.Parse("00000000-0000-0000-0000-000000000010"), Name = "Analyse-Ass",      Icon = "📊",  Rarity = AchievementRarity.Selten,   TriggerKey = "analytics_views",    TriggerValue = 10,   XpReward = 100, Description = "Analytics 10× geöffnet" }
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
