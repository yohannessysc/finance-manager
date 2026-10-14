
using FinanceManager.Domain.Entities;

namespace FinanceManager.Application.Interfaces;

public interface IFinancialAccountRepository
{
    Task<List<FinancialAccount>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<FinancialAccount?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task AddAsync(
        FinancialAccount account,
        CancellationToken ct = default);

    Task UpdateAsync(
        FinancialAccount account,
        CancellationToken ct = default);

    Task DeleteAsync(
        FinancialAccount account,
        CancellationToken ct = default);
}
