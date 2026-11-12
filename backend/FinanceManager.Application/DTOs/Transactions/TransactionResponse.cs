
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
    DateTime CreatedAtUtc,
    // Set when the transaction is split across several categories.
    IReadOnlyList<TransactionSplitResponse>? Splits = null,
    // Number of receipts attached.
    int AttachmentCount = 0
);
