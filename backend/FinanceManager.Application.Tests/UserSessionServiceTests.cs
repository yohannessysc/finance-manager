using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace FinanceManager.Application.Tests;

public sealed class UserSessionServiceTests : IDisposable
{
    private const string MacChrome =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/130.0.0.0 Safari/537.36";

    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly UserSessionService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _otherUserId = Guid.NewGuid();

    public UserSessionServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new FinanceDbContext(options);
        _db.Database.EnsureCreated();

        AddUser(_userId, "one@example.com");
        AddUser(_otherUserId, "two@example.com");
        _db.SaveChanges();

        var mockLogger = new Mock<ILogger<UserSessionService>>();
        _service = new UserSessionService(_db, mockLogger.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private void AddUser(Guid id, string email)
    {
        _db.Users.Add(new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString()
        });
    }

    private Task<UserSession> NewSession(
        Guid? userId = null,
        TimeSpan? lifetime = null)
    {
        return _service.CreateAsync(
            userId ?? _userId,
            MacChrome,
            lifetime ?? TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Create_StoresDeviceAndExpiry()
    {
        var session = await NewSession();

        Assert.Equal("Mac · Chrome", session.DeviceName);
        Assert.Equal(_userId, session.UserId);
        Assert.Null(session.RevokedAtUtc);
        Assert.True(session.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Validate_AcceptsActiveSession()
    {
        var session = await NewSession();

        Assert.True(
            await _service.ValidateAsync(session.Id, _userId));
    }

    [Fact]
    public async Task Validate_RejectsUnknownSession()
    {
        Assert.False(
            await _service.ValidateAsync(Guid.NewGuid(), _userId));
    }

    [Fact]
    public async Task Validate_RejectsSessionOwnedByAnotherUser()
    {
        var session = await NewSession();

        Assert.False(
            await _service.ValidateAsync(session.Id, _otherUserId));
    }

    [Fact]
    public async Task Validate_RejectsExpiredSession()
    {
        var session = await NewSession(
            lifetime: TimeSpan.FromMinutes(-1));

        Assert.False(
            await _service.ValidateAsync(session.Id, _userId));
    }

    [Fact]
    public async Task Validate_RejectsRevokedSession()
    {
        var session = await NewSession();

        await _service.RevokeAsync(_userId, session.Id);

        Assert.False(
            await _service.ValidateAsync(session.Id, _userId));
    }

    [Fact]
    public async Task Validate_DoesNotRewriteRecentActivity()
    {
        var session = await NewSession();
        var before = session.LastSeenAtUtc;

        await _service.ValidateAsync(session.Id, _userId);

        var stored = await _db.UserSessions
            .AsNoTracking()
            .SingleAsync(x => x.Id == session.Id);

        Assert.Equal(before, stored.LastSeenAtUtc);
    }

    [Fact]
    public async Task Validate_RefreshesStaleActivity()
    {
        var session = await NewSession();
        var stale = DateTime.UtcNow.AddMinutes(-20);

        await _db.UserSessions
            .Where(x => x.Id == session.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.LastSeenAtUtc, stale));

        await _service.ValidateAsync(session.Id, _userId);

        var stored = await _db.UserSessions
            .AsNoTracking()
            .SingleAsync(x => x.Id == session.Id);

        Assert.True(stored.LastSeenAtUtc > stale.AddMinutes(10));
    }

    [Fact]
    public async Task ListActive_ExcludesRevokedExpiredAndOtherUsers()
    {
        var active = await NewSession();
        var revoked = await NewSession();
        await NewSession(lifetime: TimeSpan.FromMinutes(-5));
        await NewSession(_otherUserId);

        await _service.RevokeAsync(_userId, revoked.Id);

        var list = await _service.ListActiveAsync(_userId);

        Assert.Single(list);
        Assert.Equal(active.Id, list[0].Id);
    }

    [Fact]
    public async Task Revoke_CannotRevokeAnotherUsersSession()
    {
        var session = await NewSession(_otherUserId);

        var result = await _service.RevokeAsync(_userId, session.Id);

        Assert.False(result);
        Assert.True(
            await _service.ValidateAsync(session.Id, _otherUserId));
    }

    [Fact]
    public async Task RevokeAll_KeepsTheExcludedSession()
    {
        var current = await NewSession();
        var second = await NewSession();
        var third = await NewSession();
        var foreign = await NewSession(_otherUserId);

        var count = await _service.RevokeAllAsync(
            _userId,
            exceptSessionId: current.Id);

        Assert.Equal(2, count);
        Assert.True(
            await _service.ValidateAsync(current.Id, _userId));
        Assert.False(
            await _service.ValidateAsync(second.Id, _userId));
        Assert.False(
            await _service.ValidateAsync(third.Id, _userId));
        Assert.True(
            await _service.ValidateAsync(foreign.Id, _otherUserId));
    }

    [Fact]
    public async Task RevokeAll_WithoutException_RevokesEverything()
    {
        var first = await NewSession();
        var second = await NewSession();

        await _service.RevokeAllAsync(_userId);

        Assert.False(await _service.ValidateAsync(first.Id, _userId));
        Assert.False(await _service.ValidateAsync(second.Id, _userId));
    }

    [Fact]
    public async Task PurgeOld_RemovesOnlyLongExpiredSessions()
    {
        var recent = await NewSession();
        var old = await NewSession(
            lifetime: TimeSpan.FromDays(-40));

        var removed = await _service.PurgeOldAsync(_userId);

        Assert.Equal(1, removed);
        var remaining = await _db.UserSessions
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync();

        Assert.Equal(new[] { recent.Id }, remaining);
        Assert.DoesNotContain(old.Id, remaining);
    }

    [Theory]
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) " +
        "AppleWebKit/605.1.15 (KHTML, like Gecko) " +
        "Version/17.0 Mobile/15E148 Safari/604.1",
        "iPhone · Safari")]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0",
        "Windows · Edge")]
    [InlineData(
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:130.0) " +
        "Gecko/20100101 Firefox/130.0",
        "Mac · Firefox")]
    [InlineData("", "Unknown device")]
    public void DescribeDevice_NamesCommonBrowsers(
        string userAgent,
        string expected)
    {
        Assert.Equal(
            expected,
            UserSessionService.DescribeDevice(userAgent));
    }
}
