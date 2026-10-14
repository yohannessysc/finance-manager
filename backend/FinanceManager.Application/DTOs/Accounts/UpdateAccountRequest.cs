
namespace FinanceManager.Application.DTOs.Accounts;

public record UpdateAccountRequest(
    string Name,
    string AccountType,
    decimal OpeningBalance
);
