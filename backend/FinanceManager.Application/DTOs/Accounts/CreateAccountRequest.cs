
namespace FinanceManager.Application.DTOs.Accounts;

public record CreateAccountRequest(
    string Name,
    string AccountType,
    decimal OpeningBalance
);
