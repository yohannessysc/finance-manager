using FinanceManager.Application.DTOs.Budgets;

namespace FinanceManager.Application.Interfaces;

public interface IBudgetService
{
    Task<List<BudgetResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<BudgetResponse> CreateAsync(
        Guid userId,
        BudgetUpsertRequest request,
        CancellationToken ct = default);

    Task<BudgetResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        BudgetUpsertRequest request,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);
}
