
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Interfaces;

public interface ITransactionCategoryRepository
{
    Task<List<TransactionCategory>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<TransactionCategory?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<bool> NameExistsAsync(
        Guid userId,
        TransactionType type,
        string name,
        Guid? excludeId = null,
        CancellationToken ct = default);

    Task<bool> IsInUseAsync(
        Guid categoryId,
        Guid userId,
        CancellationToken ct = default);

    Task AddAsync(
        TransactionCategory category,
        CancellationToken ct = default);

    Task SaveChangesAsync(
        CancellationToken ct = default);

    Task DeleteAsync(
        TransactionCategory category,
        CancellationToken ct = default);
}
