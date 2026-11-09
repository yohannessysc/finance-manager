using FinanceManager.Application.DTOs.SavingsGoals;

namespace FinanceManager.Application.Interfaces;

public interface ISavingsGoalService
{
    Task<List<SavingsGoalResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<SavingsGoalResponse> CreateAsync(
        Guid userId,
        SavingsGoalUpsertRequest request,
        CancellationToken ct = default);

    Task<SavingsGoalResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        SavingsGoalUpdateRequest request,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default);

    Task<List<SavingsGoalContributionResponse>?> GetContributionsAsync(
        Guid goalId,
        Guid userId,
        CancellationToken ct = default);

    Task<SavingsGoalResponse?> AddContributionAsync(
        Guid goalId,
        Guid userId,
        SavingsGoalContributionRequest request,
        CancellationToken ct = default);

    Task<bool> DeleteContributionAsync(
        Guid goalId,
        Guid contributionId,
        Guid userId,
        CancellationToken ct = default);
}
