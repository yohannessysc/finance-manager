
namespace FinanceManager.Application.DTOs.Dashboard;

public record CategorySpendingResponse(
    Guid? CategoryId,
    string CategoryName,
    string Currency,
    decimal Amount,
    decimal Percentage
);
