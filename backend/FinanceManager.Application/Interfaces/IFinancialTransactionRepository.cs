
using FinanceManager.Domain.Entities;

namespace FinanceManager.Application.Interfaces;

public interface IFinancialTransactionRepository
{
    Task<List<FinancialTransaction>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<FinancialTransaction?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<FinancialAccount?> GetAccountAsync(
        Guid accountId,
        Guid userId,
        CancellationToken ct = default);

    Task<TransactionCategory?> GetCategoryAsync(
        Guid categoryId,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> CanEditAccountAsync(
        Guid accountId,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> CanEditTransactionAsync(
        Guid transactionId,
        Guid userId,
        CancellationToken ct = default);

    Task AddAsync(
        FinancialTransaction transaction,
        CancellationToken ct = default);

    Task SaveChangesAsync(
        CancellationToken ct = default);

    Task DeleteAsync(
        FinancialTransaction transaction,
        CancellationToken ct = default);

    Task<List<TransactionSplit>> GetSplitsAsync(
        IReadOnlyCollection<Guid> transactionIds,
        CancellationToken ct = default);

    // Replaces the transaction's split lines; saved by SaveChangesAsync.
    Task ReplaceSplitsAsync(
        Guid transactionId,
        IEnumerable<TransactionSplit> splits,
        CancellationToken ct = default);

    Task<Dictionary<Guid, int>> GetAttachmentCountsAsync(
        IReadOnlyCollection<Guid> transactionIds,
        CancellationToken ct = default);
}
