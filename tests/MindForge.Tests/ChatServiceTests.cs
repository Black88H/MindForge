using Microsoft.EntityFrameworkCore;
using MindForge.Models;
using MindForge.Services;
using MindForge.Services.AI.Interfaces;
using MindForge.Services.AI.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace MindForge.Tests;

public class ChatServiceTests : IDisposable
{
    private readonly MindForgeDbContext _db;
    private readonly Mock<IAISelector>  _aiMock;
    private readonly ChatService        _sut;
    private readonly Guid               _userId = Guid.NewGuid();

    public ChatServiceTests()
    {
        var opts = new DbContextOptionsBuilder<MindForgeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db     = new MindForgeDbContext(opts);
        _aiMock = new Mock<IAISelector>();

        // Default: AI returns a simple response
        _aiMock.Setup(ai => ai.ExecuteAsync(It.IsAny<TaskType>(), It.IsAny<string>(), It.IsAny<string>()))
               .ReturnsAsync(new AIResponse { IsSuccess = true, Content = "KI-Antwort", ProviderName = "MockAI", TokensUsed = 10 });

        _sut = new ChatService(_db, _aiMock.Object, NullLogger<ChatService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    // ── SendMessageAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task SendMessage_SavesUserMessage()
    {
        await _sut.SendMessageAsync(_userId, null, "Hallo");

        var msgs = await _db.ChatMessages
            .Where(m => m.UserId == _userId && m.Role == ChatRole.User)
            .ToListAsync();

        msgs.Should().ContainSingle(m => m.Content == "Hallo");
    }

    [Fact]
    public async Task SendMessage_SavesAIResponse()
    {
        await _sut.SendMessageAsync(_userId, null, "Frage");

        var aiMsg = await _db.ChatMessages
            .Where(m => m.UserId == _userId && m.Role == ChatRole.Assistant)
            .FirstOrDefaultAsync();

        aiMsg.Should().NotBeNull();
        aiMsg!.Content.Should().Be("KI-Antwort");
        aiMsg.Provider.Should().Be("MockAI");
        aiMsg.TokensUsed.Should().Be(10);
    }

    [Fact]
    public async Task SendMessage_CallsAISelector_Once()
    {
        await _sut.SendMessageAsync(_userId, null, "Test");

        _aiMock.Verify(ai => ai.ExecuteAsync(It.IsAny<TaskType>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_WithSubject_SavesSubjectId()
    {
        var subjectId = Guid.NewGuid();
        _db.Subjects.Add(new Subject { Id = subjectId, Name = "Mathe", Icon = "∫", Color = "#fff" });
        await _db.SaveChangesAsync();

        await _sut.SendMessageAsync(_userId, subjectId, "Frage zu Mathe");

        var msgs = _db.ChatMessages.Where(m => m.UserId == _userId).ToList();
        msgs.Should().AllSatisfy(m => m.SubjectId.Should().Be(subjectId));
    }

    [Fact]
    public async Task SendMessage_ReturnsAIMessage()
    {
        var result = await _sut.SendMessageAsync(_userId, null, "Test");

        result.Role.Should().Be(ChatRole.Assistant);
        result.Content.Should().Be("KI-Antwort");
    }

    // ── GetChatHistoryAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetChatHistory_ReturnsMessagesInChronologicalOrder()
    {
        var base_ = DateTime.UtcNow.AddMinutes(-10);
        _db.ChatMessages.AddRange(
            new ChatMessage { UserId = _userId, Role = ChatRole.User,      Content = "Erste",  CreatedAt = base_ },
            new ChatMessage { UserId = _userId, Role = ChatRole.Assistant, Content = "Zweite", CreatedAt = base_.AddMinutes(1) },
            new ChatMessage { UserId = _userId, Role = ChatRole.User,      Content = "Dritte", CreatedAt = base_.AddMinutes(2) }
        );
        await _db.SaveChangesAsync();

        var history = await _sut.GetChatHistoryAsync(_userId, null);

        history.Should().HaveCount(3);
        history[0].Content.Should().Be("Erste");
        history[1].Content.Should().Be("Zweite");
        history[2].Content.Should().Be("Dritte");
    }

    [Fact]
    public async Task GetChatHistory_RespectsSubjectFilter()
    {
        var subjectA = Guid.NewGuid();
        var subjectB = Guid.NewGuid();

        _db.ChatMessages.AddRange(
            new ChatMessage { UserId = _userId, SubjectId = subjectA, Role = ChatRole.User, Content = "SubjA" },
            new ChatMessage { UserId = _userId, SubjectId = subjectB, Role = ChatRole.User, Content = "SubjB" },
            new ChatMessage { UserId = _userId, SubjectId = null,     Role = ChatRole.User, Content = "Kein Fach" }
        );
        await _db.SaveChangesAsync();

        var historyA = await _sut.GetChatHistoryAsync(_userId, subjectA);

        historyA.Should().ContainSingle(m => m.Content == "SubjA");
        historyA.Should().NotContain(m => m.Content == "SubjB");
    }

    [Fact]
    public async Task GetChatHistory_RespectsSkipAndTake()
    {
        for (int i = 0; i < 10; i++)
        {
            _db.ChatMessages.Add(new ChatMessage
            {
                UserId    = _userId,
                Role      = ChatRole.User,
                Content   = $"Nachricht {i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(i)
            });
        }
        await _db.SaveChangesAsync();

        var page = await _sut.GetChatHistoryAsync(_userId, null, skip: 5, take: 3);

        page.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetChatHistory_EmptyWhenNoMessages()
    {
        var history = await _sut.GetChatHistoryAsync(_userId, null);
        history.Should().BeEmpty();
    }

    // ── ClearChatAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ClearChat_RemovesAllMessagesForUser()
    {
        _db.ChatMessages.AddRange(
            new ChatMessage { UserId = _userId, Role = ChatRole.User,      Content = "A" },
            new ChatMessage { UserId = _userId, Role = ChatRole.Assistant, Content = "B" }
        );
        await _db.SaveChangesAsync();

        await _sut.ClearChatAsync(_userId, null);

        _db.ChatMessages.Where(m => m.UserId == _userId).Should().BeEmpty();
    }

    [Fact]
    public async Task ClearChat_DoesNotRemoveOtherUsersMessages()
    {
        var otherUserId = Guid.NewGuid();
        _db.ChatMessages.AddRange(
            new ChatMessage { UserId = _userId,    Role = ChatRole.User, Content = "Mine" },
            new ChatMessage { UserId = otherUserId, Role = ChatRole.User, Content = "Theirs" }
        );
        await _db.SaveChangesAsync();

        await _sut.ClearChatAsync(_userId, null);

        _db.ChatMessages.Should().ContainSingle(m => m.Content == "Theirs");
    }

    [Fact]
    public async Task ClearChat_RespectsSubjectFilter()
    {
        var subjId = Guid.NewGuid();
        _db.ChatMessages.AddRange(
            new ChatMessage { UserId = _userId, SubjectId = subjId, Role = ChatRole.User, Content = "Mit Fach" },
            new ChatMessage { UserId = _userId, SubjectId = null,   Role = ChatRole.User, Content = "Ohne Fach" }
        );
        await _db.SaveChangesAsync();

        await _sut.ClearChatAsync(_userId, subjId);

        _db.ChatMessages.Should().ContainSingle(m => m.Content == "Ohne Fach");
        _db.ChatMessages.Should().NotContain(m => m.Content == "Mit Fach");
    }
}
