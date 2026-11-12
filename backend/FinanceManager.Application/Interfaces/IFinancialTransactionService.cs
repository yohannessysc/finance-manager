
using FinanceManager.Application.DTOs.Transactions;

namespace FinanceManager.Application.Interfaces;

public interface IFinancialTransactionService
{
    Task<List<TransactionResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<TransactionResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<TransactionResponse> CreateAsync(
        Guid userId,
        CreateTransactionRequest request,
        CancellationToken ct = default);

    Task<TransactionResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateTransactionRequest request,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    // Splits a transaction across categories; an empty list removes the
    // split. Returns null when the transaction is not found or not editable.
    Task<TransactionResponse?> SetSplitsAsync(
        Guid id,
        Guid userId,
        SetSplitsRequest request,
        CancellationToken ct = default);
}
