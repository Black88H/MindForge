using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;

namespace MindForge.Tests;

public class GamificationServiceTests : IDisposable
{
    private readonly MindForgeDbContext _db;
    private readonly GamificationService _sut;
    private readonly User _user;

    public GamificationServiceTests()
    {
        var opts = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MindForgeDbContext(opts);
        _sut = new GamificationService(_db);

        _user = new User { Username = "testuser", Email = "test@test.de", Level = 1, TotalXP = 0 };
        _db.Users.Add(_user);
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── GetLevelForXP (pure function) ─────────────────────────────────────

    [Theory]
    [InlineData(0,   1)]
    [InlineData(50,  1)]
    [InlineData(99,  1)]
    [InlineData(100, 1)]  // level 2 threshold = round(100 * 2^1.5) ≈ 283
    [InlineData(283, 2)]
    [InlineData(520, 3)]  // round(100 * 3^1.5) ≈ 520
    public void GetLevelForXP_ReturnsCorrectLevel(int xp, int expectedLevel)
    {
        var level = _sut.GetLevelForXP(xp);
        level.Should().Be(expectedLevel);
    }

    [Fact]
    public void GetLevelForXP_AlwaysAtLeastOne()
    {
        _sut.GetLevelForXP(0).Should().BeGreaterThanOrEqualTo(1);
    }

    // ── GetXPForLevel (pure function) ─────────────────────────────────────

    [Theory]
    [InlineData(1, 100)]  // round(100 * 1^1.5) = 100
    [InlineData(2, 283)]  // round(100 * 2^1.5) ≈ 283
    [InlineData(3, 520)]  // round(100 * 3^1.5) ≈ 520
    public void GetXPForLevel_ReturnsExpectedThreshold(int level, int expectedXP)
    {
        var xp = _sut.GetXPForLevel(level);
        xp.Should().Be(expectedXP);
    }

    [Fact]
    public void GetXPForLevel_IsMonotonicallyIncreasing()
    {
        for (int level = 1; level < 20; level++)
            _sut.GetXPForLevel(level + 1).Should().BeGreaterThan(_sut.GetXPForLevel(level));
    }

    // ── AwardXPAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task AwardXP_IncreasesUserTotalXP()
    {
        await _sut.AwardXPAsync(_user.Id, 50, XPSource.TestCompleted, "Test");

        var updated = await _db.Users.FindAsync(_user.Id);
        updated!.TotalXP.Should().Be(50);
    }

    [Fact]
    public async Task AwardXP_MultipleAwards_Accumulate()
    {
        await _sut.AwardXPAsync(_user.Id, 30, XPSource.TestCompleted, "A");
        await _sut.AwardXPAsync(_user.Id, 20, XPSource.LessonCompleted, "B");

        var updated = await _db.Users.FindAsync(_user.Id);
        updated!.TotalXP.Should().Be(50);
    }

    [Fact]
    public async Task AwardXP_CreatesXPEvent()
    {
        await _sut.AwardXPAsync(_user.Id, 75, XPSource.FeynmanPassed, "Feynman Test");

        var events = _db.XPEvents.Where(e => e.UserId == _user.Id).ToList();
        events.Should().ContainSingle();
        events[0].Amount.Should().Be(75);
        events[0].Source.Should().Be(XPSource.FeynmanPassed);
    }

    [Fact]
    public async Task AwardXP_TriggersLevelUp_WhenThresholdCrossed()
    {
        // Level 2 threshold = 283 XP
        await _sut.AwardXPAsync(_user.Id, 300, XPSource.TestCompleted, "Großes Award");

        var updated = await _db.Users.FindAsync(_user.Id);
        updated!.Level.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task AwardXP_DoesNotDegradeLevel_OnSmallAmount()
    {
        _user.Level = 5;
        _user.TotalXP = 1000;
        await _db.SaveChangesAsync();

        await _sut.AwardXPAsync(_user.Id, 10, XPSource.LessonCompleted, "Kleines Award");

        var updated = await _db.Users.FindAsync(_user.Id);
        updated!.Level.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public async Task AwardXP_UnknownUser_ThrowsArgumentException()
    {
        var act = async () => await _sut.AwardXPAsync(Guid.NewGuid(), 50, XPSource.TestCompleted, "x");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── UpdateStreakAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStreak_FirstActivity_SetsStreak1()
    {
        var (_, streak) = await _sut.UpdateStreakAsync(_user.Id);
        streak.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStreak_ConsecutiveDays_IncrementsStreak()
    {
        // Add an XP event "yesterday"
        _db.XPEvents.Add(new XPEvent
        {
            UserId    = _user.Id,
            Amount    = 10,
            Source    = XPSource.LessonCompleted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        _user.CurrentStreak = 1;
        await _db.SaveChangesAsync();

        var (continued, streak) = await _sut.UpdateStreakAsync(_user.Id);

        continued.Should().BeTrue();
        streak.Should().Be(2);
    }

    [Fact]
    public async Task UpdateStreak_SameDay_ReturnsCurrentStreakUnchanged()
    {
        _db.XPEvents.Add(new XPEvent
        {
            UserId    = _user.Id,
            Amount    = 10,
            Source    = XPSource.LessonCompleted,
            CreatedAt = DateTime.UtcNow  // today
        });
        _user.CurrentStreak = 3;
        await _db.SaveChangesAsync();

        var (continued, streak) = await _sut.UpdateStreakAsync(_user.Id);

        continued.Should().BeTrue();
        streak.Should().Be(3);
    }

    [Fact]
    public async Task UpdateStreak_GapInDays_ResetsStreak()
    {
        _db.XPEvents.Add(new XPEvent
        {
            UserId    = _user.Id,
            Amount    = 10,
            Source    = XPSource.LessonCompleted,
            CreatedAt = DateTime.UtcNow.AddDays(-3)  // 3 days ago — gap
        });
        _user.CurrentStreak = 10;
        await _db.SaveChangesAsync();

        var (continued, streak) = await _sut.UpdateStreakAsync(_user.Id);

        continued.Should().BeFalse();
        streak.Should().Be(1);
    }

    [Fact]
    public async Task UpdateStreak_LongestStreak_Updated_WhenCurrentExceeds()
    {
        _db.XPEvents.Add(new XPEvent
        {
            UserId    = _user.Id,
            Amount    = 10,
            Source    = XPSource.LessonCompleted,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        _user.CurrentStreak  = 5;
        _user.LongestStreak  = 5;
        await _db.SaveChangesAsync();

        await _sut.UpdateStreakAsync(_user.Id);

        var updated = await _db.Users.FindAsync(_user.Id);
        updated!.LongestStreak.Should().Be(6);
    }

    // ── GetStatsAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetStats_UnknownUser_ReturnsZeroStats()
    {
        var stats = await _sut.GetStatsAsync(Guid.NewGuid());

        stats.Level.Should().Be(0);
        stats.TotalXP.Should().Be(0);
    }

    [Fact]
    public async Task GetStats_ReflectsCurrentUserValues()
    {
        _user.TotalXP      = 200;
        _user.Level        = 2;
        _user.CurrentStreak = 7;
        await _db.SaveChangesAsync();

        var stats = await _sut.GetStatsAsync(_user.Id);

        stats.TotalXP.Should().Be(200);
        stats.Level.Should().Be(2);
        stats.CurrentStreak.Should().Be(7);
    }
}
