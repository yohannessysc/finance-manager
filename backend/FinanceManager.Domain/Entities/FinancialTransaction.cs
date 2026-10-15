
using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities;

public class FinancialTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid FinancialAccountId { get; set; }

    public Guid? DestinationAccountId { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? RecurringTransactionId { get; set; }
    public DateTime? ScheduledOccurrenceUtc { get; set; }
    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? Merchant { get; set; }

    public DateTime TransactionDateUtc { get; set; } =
        DateTime.UtcNow;

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}
