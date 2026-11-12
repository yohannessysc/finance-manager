using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

public sealed class UserSessionService
{
    public const string SessionClaimType = "fm_sid";

    // Matches the authentication cookie lifetime.
    public static readonly TimeSpan Lifetime =
        TimeSpan.FromMinutes(30);

    // Sessions slide on activity (idle timeout = Lifetime) but can never
    // outlive this absolute cap from the original login.
    public static readonly TimeSpan AbsoluteLifetime =
        TimeSpan.FromHours(12);

    // Activity is written at most this often per session.
    private static readonly TimeSpan TouchInterval =
        TimeSpan.FromMinutes(5);

    private readonly FinanceDbContext _db;
    private readonly ILogger<UserSessionService> _logger;

    public UserSessionService(
        FinanceDbContext db,
        ILogger<UserSessionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UserSession> CreateAsync(
        Guid userId,
        string? userAgent,
        TimeSpan lifetime,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var agent = Truncate(userAgent, 500);

        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
            UserAgent = agent,
            DeviceName = DescribeDevice(agent)
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return session;
    }

    // True only for a session that belongs to the user, is not
    // revoked and has not expired. Also refreshes LastSeenAtUtc
    // periodically instead of on every request.
    public async Task<bool> ValidateAsync(
        Guid sessionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var session = await _db.UserSessions
            .AsNoTracking()
            .Where(x => x.Id == sessionId && x.UserId == userId)
            .Select(x => new
            {
                x.RevokedAtUtc,
                x.ExpiresAtUtc,
                x.LastSeenAtUtc,
                x.CreatedAtUtc
            })
            .SingleOrDefaultAsync(ct);

        if (session is null)
        {
            _logger.LogWarning(
                "ValidateAsync: Session not found for sessionId={sessionId}, userId={userId}",
                sessionId,
                userId);
            return false;
        }

        if (session.RevokedAtUtc is not null)
        {
            _logger.LogWarning(
                "ValidateAsync: Session revoked at {revokedAt}",
                session.RevokedAtUtc);
            return false;
        }

        if (session.ExpiresAtUtc <= now)
        {
            _logger.LogWarning(
                "ValidateAsync: Session expired. ExpiresAt={expiresAt}, Now={now}",
                session.ExpiresAtUtc,
                now);
            return false;
        }

        _logger.LogInformation(
            "ValidateAsync: Session valid");

        if (now - session.LastSeenAtUtc >= TouchInterval)
        {
            // Sliding idle timeout, capped by the absolute lifetime.
            var absoluteEnd = session.CreatedAtUtc + AbsoluteLifetime;
            var newExpiry = now + Lifetime;
            if (newExpiry > absoluteEnd) newExpiry = absoluteEnd;

            await _db.UserSessions
                .Where(x => x.Id == sessionId)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(x => x.LastSeenAtUtc, now)
                        .SetProperty(x => x.ExpiresAtUtc, newExpiry),
                    ct);
        }

        return true;
    }

    public async Task<List<UserSession>> ListActiveAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await _db.UserSessions
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.RevokedAtUtc == null &&
                        x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.LastSeenAtUtc)
            .ToListAsync(ct);
    }

    public async Task<bool> RevokeAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var affected = await _db.UserSessions
            .Where(x => x.Id == sessionId &&
                        x.UserId == userId &&
                        x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAtUtc, now),
                ct);

        return affected > 0;
    }

    // Revokes every active session, optionally keeping one.
    public async Task<int> RevokeAllAsync(
        Guid userId,
        Guid? exceptSessionId = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await _db.UserSessions
            .Where(x => x.UserId == userId &&
                        x.RevokedAtUtc == null &&
                        (exceptSessionId == null ||
                         x.Id != exceptSessionId))
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.RevokedAtUtc, now),
                ct);
    }

    // Removes long-expired history so the table stays small.
    public async Task<int> PurgeOldAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        return await _db.UserSessions
            .Where(x => x.UserId == userId &&
                        x.ExpiresAtUtc < cutoff)
            .ExecuteDeleteAsync(ct);
    }

    public static string DescribeDevice(string userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
            return "Unknown device";

        var ua = userAgent;

        var os =
            ua.Contains("iPhone") ? "iPhone" :
            ua.Contains("iPad") ? "iPad" :
            ua.Contains("Android") ? "Android" :
            ua.Contains("Windows") ? "Windows" :
            ua.Contains("Mac OS X") ? "Mac" :
            ua.Contains("CrOS") ? "ChromeOS" :
            ua.Contains("Linux") ? "Linux" :
            "Unknown OS";

        // Order matters: Edge and Opera also contain "Chrome",
        // and Chrome contains "Safari".
        var browser =
            ua.Contains("Edg/") ? "Edge" :
            ua.Contains("OPR/") ? "Opera" :
            ua.Contains("Firefox/") || ua.Contains("FxiOS/") ? "Firefox" :
            ua.Contains("Chrome/") || ua.Contains("CriOS/") ? "Chrome" :
            ua.Contains("Safari/") ? "Safari" :
            "Browser";

        return $"{os} · {browser}";
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= max ? value : value[..max];
    }
}
