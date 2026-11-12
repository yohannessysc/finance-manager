
namespace FinanceManager.Application.DTOs.Accounts;

public record FinancialAccountResponse(
    Guid Id,
    string Name,
    string AccountType,
    string Currency,
    decimal OpeningBalance,
    DateTime CreatedAtUtc,
    Guid? HouseholdId = null,
    bool IsOwner = false,
    // Synced from a bank connection; statement imports are not allowed.
    bool IsBankLinked = false
);
