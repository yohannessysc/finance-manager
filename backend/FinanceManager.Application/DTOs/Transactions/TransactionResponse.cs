
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Transactions;

public record TransactionResponse(
    Guid Id,
    Guid FinancialAccountId,
    Guid? DestinationAccountId,
    Guid? CategoryId,
    Guid? RecurringTransactionId,
    TransactionType Type,
    decimal Amount,
    string Description,
    string? Merchant,
    DateTime TransactionDateUtc,
    DateTime CreatedAtUtc
);
