
namespace FinanceManager.Application.DTOs.Auth;

public record AuthUserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email
);
