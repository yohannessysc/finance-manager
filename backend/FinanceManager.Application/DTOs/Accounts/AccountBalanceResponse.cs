
namespace FinanceManager.Application.DTOs.Accounts;

public record AccountBalanceResponse(
    Guid AccountId,
    string AccountName,
    string Currency,
    decimal OpeningBalance,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal IncomingTransfers,
    decimal OutgoingTransfers,
    decimal CurrentBalance
);
