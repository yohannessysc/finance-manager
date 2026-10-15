
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Transactions;

public record CreateTransactionRequest(
    Guid FinancialAccountId,
    Guid? DestinationAccountId,
    Guid? CategoryId,
    TransactionType Type,
    decimal Amount,
    string Description,
    string? Merchant,
    DateTime TransactionDateUtc
);
