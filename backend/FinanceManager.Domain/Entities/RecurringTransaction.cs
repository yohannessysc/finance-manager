
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities;

public class RecurringTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid FinancialAccountId { get; set; }

    public Guid? DestinationAccountId { get; set; }

    public Guid? CategoryId { get; set; }

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public RecurrenceFrequency Frequency { get; set; }

    public RecurringStatus Status { get; set; } =
        RecurringStatus.Active;

    public string TimeZoneId { get; set; } = "Etc/UTC";

    public DateTime StartDateUtc { get; set; }

    public DateTime NextOccurrenceUtc { get; set; }

    public DateTime? EndDateUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}
