using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Households;

public sealed record HouseholdResponse(
    Guid Id,
    string Name,
    Guid OwnerUserId,
    DateTime CreatedAtUtc,
    bool CanManage,
    bool CanEdit,
    bool CanAssignAdmin,
    List<HouseholdMemberResponse> Members);

public sealed record HouseholdMemberResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    HouseholdRole Role,
    DateTime JoinedAtUtc);

public sealed record HouseholdInvitationResponse(
    Guid Id,
    string Email,
    HouseholdRole Role,
    DateTime ExpiresAtUtc);
