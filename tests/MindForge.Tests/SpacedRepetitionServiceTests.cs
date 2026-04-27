using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;

namespace MindForge.Tests;

public class SpacedRepetitionServiceTests : IDisposable
{
    private readonly MindForgeDbContext _db;
    private readonly SpacedRepetitionService _sut;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _nodeId = Guid.NewGuid();

    public SpacedRepetitionServiceTests()
    {
        var opts = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MindForgeDbContext(opts);
        _sut = new SpacedRepetitionService(_db);

        // Seed a KnowledgeNode so FK is satisfied
        _db.KnowledgeNodes.Add(new KnowledgeNode
        {
            Id = _nodeId,
            UserId = _userId,
            SubjectId = Guid.NewGuid(),
            Title = "Testknoten"
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    // ── ScheduleReviewAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ScheduleReviewAsync_CreatesNewItem_WithDefaultValues()
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);

        item.Should().NotBeNull();
        item.EaseFactor.Should().BeApproximately(2.5f, 0.001f);
        item.Interval.Should().Be(1);
        item.Repetitions.Should().Be(0);
    }

    [Fact]
    public async Task ScheduleReviewAsync_ReturnsExisting_WhenCalledTwice()
    {
        var first  = await _sut.ScheduleReviewAsync(_userId, _nodeId);
        var second = await _sut.ScheduleReviewAsync(_userId, _nodeId);

        second.Id.Should().Be(first.Id);
    }

    // ── RecordReviewAsync – quality < 3 (fail) ─────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RecordReview_QualityBelow3_ResetsRepetitionsAndInterval(int quality)
    {
        // Create an item that has already progressed
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);
        item.Repetitions = 5;
        item.Interval    = 42;
        await _db.SaveChangesAsync();

        await _sut.RecordReviewAsync(item.Id, quality);

        var updated = await _db.Set<SpacedRepetitionItem>().FindAsync(item.Id);
        updated!.Repetitions.Should().Be(0);
        updated.Interval.Should().Be(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RecordReview_QualityBelow3_EaseFactorNeverBelowMinimum(int quality)
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);
        // Force low EF close to minimum
        item.EaseFactor = 1.3f;
        await _db.SaveChangesAsync();

        await _sut.RecordReviewAsync(item.Id, quality);

        var updated = await _db.Set<SpacedRepetitionItem>().FindAsync(item.Id);
        updated!.EaseFactor.Should().BeGreaterThanOrEqualTo(1.3f);
    }

    // ── RecordReviewAsync – quality >= 3 (pass) ────────────────────────────

    [Fact]
    public async Task RecordReview_FirstRepetition_SetsInterval1()
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);

        await _sut.RecordReviewAsync(item.Id, 4);

        var updated = await _db.Set<SpacedRepetitionItem>().FindAsync(item.Id);
        updated!.Repetitions.Should().Be(1);
        updated.Interval.Should().Be(1);
    }

    [Fact]
    public async Task RecordReview_SecondRepetition_SetsInterval6()
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);
        // advance to rep=1
        item.Repetitions = 1;
        item.Interval    = 1;
        await _db.SaveChangesAsync();

        await _sut.RecordReviewAsync(item.Id, 4);

        var updated = await _db.Set<SpacedRepetitionItem>().FindAsync(item.Id);
        updated!.Repetitions.Should().Be(2);
        updated.Interval.Should().Be(6);
    }

    [Fact]
    public async Task RecordReview_ThirdRepetition_UsesEaseFactorFormula()
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);
        item.Repetitions = 2;
        item.Interval    = 6;
        item.EaseFactor  = 2.5f;
        await _db.SaveChangesAsync();

        await _sut.RecordReviewAsync(item.Id, 5); // perfect recall

        var updated = await _db.Set<SpacedRepetitionItem>().FindAsync(item.Id);
        // interval = round(6 * 2.5) = 15, but EF is first updated, use old EF for interval calc
        updated!.Repetitions.Should().Be(3);
        updated.Interval.Should().Be(15);
    }

    // ── EaseFactor formula verification ────────────────────────────────────

    [Theory]
    [InlineData(5, 2.5f)]  // quality=5, EF increases
    [InlineData(3, 2.5f)]  // quality=3, EF decreases slightly
    [InlineData(0, 1.3f)]  // worst quality, already at min → stays 1.3
    public async Task RecordReview_EaseFactorUpdatedCorrectly(int quality, float startEF)
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);
        item.EaseFactor = startEF;
        await _db.SaveChangesAsync();

        await _sut.RecordReviewAsync(item.Id, quality);

        var updated = await _db.Set<SpacedRepetitionItem>().FindAsync(item.Id);
        updated!.EaseFactor.Should().BeGreaterThanOrEqualTo(1.3f);

        if (quality == 5)
            updated.EaseFactor.Should().BeGreaterThan(startEF);
        else if (quality == 3)
            updated.EaseFactor.Should().BeLessThan(startEF);
    }

    [Fact]
    public async Task RecordReview_EaseFactorNeverDropsBelowOnePointThree_AfterManyFailures()
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);

        // Simulate 10 consecutive failures
        for (int i = 0; i < 10; i++)
            await _sut.RecordReviewAsync(item.Id, 0);

        var updated = await _db.Set<SpacedRepetitionItem>().FindAsync(item.Id);
        updated!.EaseFactor.Should().BeGreaterThanOrEqualTo(1.3f);
    }

    // ── GetDueReviewsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetDueReviews_ReturnsDueItems()
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);
        item.NextReviewDate = DateTime.UtcNow.Date.AddDays(-1); // due yesterday
        await _db.SaveChangesAsync();

        var due = await _sut.GetDueReviewsAsync(_userId);

        due.Should().ContainSingle(i => i.Id == item.Id);
    }

    [Fact]
    public async Task GetDueReviews_ExcludesFutureItems()
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);
        item.NextReviewDate = DateTime.UtcNow.Date.AddDays(5); // not due yet
        await _db.SaveChangesAsync();

        var due = await _sut.GetDueReviewsAsync(_userId);

        due.Should().BeEmpty();
    }

    // ── Quality clamping ──────────────────────────────────────────────────

    [Fact]
    public async Task RecordReview_QualityClamped_DoesNotThrow()
    {
        var item = await _sut.ScheduleReviewAsync(_userId, _nodeId);

        var act1 = async () => await _sut.RecordReviewAsync(item.Id, -5);
        var act2 = async () => await _sut.RecordReviewAsync(item.Id, 99);

        await act1.Should().NotThrowAsync();
        await act2.Should().NotThrowAsync();
    }

    // ── Error cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task RecordReview_NonExistentItem_ThrowsArgumentException()
    {
        var act = async () => await _sut.RecordReviewAsync(Guid.NewGuid(), 4);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
