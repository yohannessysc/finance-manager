
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Transactions;

public record UpdateTransactionRequest(
    Guid FinancialAccountId,
    Guid? DestinationAccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    string Description,
    string? Merchant,
    DateTime TransactionDateUtc
);
