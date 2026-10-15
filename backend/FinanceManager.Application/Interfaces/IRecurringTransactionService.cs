
using FinanceManager.Application.DTOs.RecurringTransactions;

namespace FinanceManager.Application.Interfaces;

public interface IRecurringTransactionService
{
    Task<List<RecurringTransactionResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<RecurringTransactionResponse?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<RecurringTransactionResponse> CreateAsync(
        Guid userId,
        CreateRecurringTransactionRequest request,
        CancellationToken ct = default);

    Task<RecurringTransactionResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateRecurringTransactionRequest request,
        CancellationToken ct = default);

    Task<bool> PauseAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> ResumeAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> CancelAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);
}
