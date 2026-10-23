
namespace FinanceManager.Application.DTOs.Dashboard;

public record MonthlyCashFlowResponse(
    int Year,
    int Month,
    string Currency,
    decimal TotalIncome,
    decimal TotalExpenses,
    decimal NetCashFlow
);
