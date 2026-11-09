using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.Interfaces;

public interface IHouseholdAuthorizationService
{
    Task<List<Guid>> GetHouseholdIdsAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<bool> IsMemberAsync(
        Guid userId,
        Guid householdId,
        CancellationToken ct = default);

    Task<bool> CanEditAsync(
        Guid userId,
        Guid householdId,
        CancellationToken ct = default);

    Task<bool> CanManageAsync(
        Guid userId,
        Guid householdId,
        CancellationToken ct = default);

    Task<HouseholdRole?> GetRoleAsync(
        Guid userId,
        Guid householdId,
        CancellationToken ct = default);
}
