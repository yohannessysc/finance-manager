using FinanceManager.Application.DTOs.Households;

namespace FinanceManager.Application.Interfaces;

public interface IHouseholdService
{
    Task<List<HouseholdResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<HouseholdResponse> CreateAsync(
        Guid userId,
        CreateHouseholdRequest request,
        CancellationToken ct = default);

    Task<HouseholdInvitationResponse> InviteAsync(
        Guid userId,
        Guid householdId,
        InviteHouseholdMemberRequest request,
        CancellationToken ct = default);

    Task<bool> AcceptInvitationAsync(
        Guid userId,
        AcceptHouseholdInvitationRequest request,
        CancellationToken ct = default);

    Task<bool> RemoveMemberAsync(
        Guid userId,
        Guid householdId,
        Guid memberUserId,
        CancellationToken ct = default);

    Task<bool> UpdateMemberRoleAsync(
        Guid userId,
        Guid householdId,
        Guid memberUserId,
        UpdateHouseholdMemberRoleRequest request,
        CancellationToken ct = default);
}
