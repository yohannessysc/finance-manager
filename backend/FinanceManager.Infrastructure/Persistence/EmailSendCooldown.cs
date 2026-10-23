
namespace FinanceManager.Infrastructure.Persistence;

public sealed class EmailSendCooldown
{
    public Guid UserId { get; set; }

    public string Purpose { get; set; } = string.Empty;

    public DateTime LastSentAtUtc { get; set; }
}
