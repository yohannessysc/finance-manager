
namespace FinanceManager.Application.DTOs.Accounts;

public record FinancialAccountResponse(
    Guid Id,
    string Name,
    string AccountType,
    string Currency,
    decimal OpeningBalance,
    DateTime CreatedAtUtc
);
