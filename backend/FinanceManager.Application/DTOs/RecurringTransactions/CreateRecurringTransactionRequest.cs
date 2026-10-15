
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.RecurringTransactions;

public record CreateRecurringTransactionRequest(
    Guid FinancialAccountId,
    Guid? DestinationAccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    string Description,
    RecurrenceFrequency Frequency,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    string TimeZoneId
);
