
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.RecurringTransactions;

public record RecurringTransactionResponse(
    Guid Id,
    Guid FinancialAccountId,
    Guid? DestinationAccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    string Description,
    RecurrenceFrequency Frequency,
    RecurringStatus Status,
    DateTime StartDateUtc,
    DateTime NextOccurrenceUtc,
    DateTime? EndDateUtc,
    DateTime CreatedAtUtc,
    string TimeZoneId
);
