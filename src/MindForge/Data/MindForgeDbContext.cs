using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using System;

namespace MindForge.Data;

public class MindForgeDbContext : DbContext
{
    public MindForgeDbContext()
    {
    }

    public MindForgeDbContext(DbContextOptions<MindForgeDbContext> options)
        : base(options)
    {
    }
    
    // DbSets
    public DbSet<User> Users => Set<User>();
    public DbSet<Subject> Subjects => Set<Subject>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<NotebookSession> NotebookSessions => Set<NotebookSession>();
    public DbSet<KnowledgeNode> KnowledgeNodes => Set<KnowledgeNode>();
    public DbSet<KnowledgeEdge> KnowledgeEdges => Set<KnowledgeEdge>();
    public DbSet<LearningPlan> LearningPlans => Set<LearningPlan>();
    public DbSet<LearningTask> LearningTasks => Set<LearningTask>();
    public DbSet<SpacedRepetitionItem> SpacedRepetitionItems => Set<SpacedRepetitionItem>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<FeynmanSession> FeynmanSessions => Set<FeynmanSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<XPEvent> XPEvents => Set<XPEvent>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<Achievement> Achievements => Set<Achievement>();
    public DbSet<UserAchievement> UserAchievements => Set<UserAchievement>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=mindforge.db");
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // AppSettings Primary Key
        modelBuilder.Entity<AppSettings>().HasKey(a => a.Key);

        // KnowledgeEdge: Zwei FK zur selben Tabelle
        modelBuilder.Entity<KnowledgeEdge>()
            .HasOne(e => e.FromNode)
            .WithMany(n => n.OutgoingEdges)
            .HasForeignKey(e => e.FromNodeId)
            .OnDelete(DeleteBehavior.Restrict);
            
        modelBuilder.Entity<KnowledgeEdge>()
            .HasOne(e => e.ToNode)
            .WithMany(n => n.IncomingEdges)
            .HasForeignKey(e => e.ToNodeId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Seed-Daten: 10 Badges
        modelBuilder.Entity<Badge>().HasData(
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000001"), 
                Name = "Erster Upload", 
                Description = "Erstes Material hochgeladen", 
                IconKey = "upload", 
                Requirement = "{\"type\":\"materials_uploaded\",\"count\":1}", 
                XPReward = 50 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000002"), 
                Name = "Wissensdurst", 
                Description = "10 Materialien hochgeladen", 
                IconKey = "books", 
                Requirement = "{\"type\":\"materials_uploaded\",\"count\":10}", 
                XPReward = 200 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000003"), 
                Name = "Testpilot", 
                Description = "Ersten Test abgeschlossen", 
                IconKey = "test", 
                Requirement = "{\"type\":\"tests_completed\",\"count\":1}", 
                XPReward = 50 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000004"), 
                Name = "Perfektionist", 
                Description = "100% in einem Test", 
                IconKey = "star", 
                Requirement = "{\"type\":\"perfect_score\",\"count\":1}", 
                XPReward = 200 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000005"), 
                Name = "Feynman-Meister", 
                Description = "5 Feynman-Sessions bestanden", 
                IconKey = "brain", 
                Requirement = "{\"type\":\"feynman_passed\",\"count\":5}", 
                XPReward = 300 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000006"), 
                Name = "Wochenstreak", 
                Description = "7 Tage in Folge gelernt", 
                IconKey = "fire", 
                Requirement = "{\"type\":\"streak\",\"count\":7}", 
                XPReward = 100 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000007"), 
                Name = "Monatsstreak", 
                Description = "30 Tage in Folge gelernt", 
                IconKey = "fire2", 
                Requirement = "{\"type\":\"streak\",\"count\":30}", 
                XPReward = 500 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000008"), 
                Name = "KI-Fl�sterer", 
                Description = "50 Chat-Nachrichten gesendet", 
                IconKey = "chat", 
                Requirement = "{\"type\":\"chat_messages\",\"count\":50}", 
                XPReward = 150 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000009"), 
                Name = "Planer", 
                Description = "Ersten Lernplan erstellt", 
                IconKey = "calendar", 
                Requirement = "{\"type\":\"plans_created\",\"count\":1}", 
                XPReward = 50 
            },
            new Badge 
            { 
                Id = Guid.Parse("b0000001-0000-0000-0000-000000000010"), 
                Name = "Level 10", 
                Description = "Level 10 erreicht", 
                IconKey = "trophy", 
                Requirement = "{\"type\":\"level\",\"count\":10}", 
                XPReward = 1000 
            }
        );
    }
}

