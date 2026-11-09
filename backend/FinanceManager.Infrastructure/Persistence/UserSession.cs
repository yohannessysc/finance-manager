namespace FinanceManager.Infrastructure.Persistence;

public sealed class UserSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public string UserAgent { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;
}
