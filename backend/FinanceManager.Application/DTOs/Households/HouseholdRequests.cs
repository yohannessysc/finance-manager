namespace FinanceManager.Application.DTOs.Households;

public sealed record CreateHouseholdRequest(string Name);

public sealed record InviteHouseholdMemberRequest(
    string Email,
    string Role);

public sealed record AcceptHouseholdInvitationRequest(string Token);

public sealed record UpdateHouseholdMemberRoleRequest(string Role);
