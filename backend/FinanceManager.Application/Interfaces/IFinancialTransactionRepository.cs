
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

    Task AddAsync(
        FinancialTransaction transaction,
        CancellationToken ct = default);

    Task SaveChangesAsync(
        CancellationToken ct = default);

    Task DeleteAsync(
        FinancialTransaction transaction,
        CancellationToken ct = default);
}
