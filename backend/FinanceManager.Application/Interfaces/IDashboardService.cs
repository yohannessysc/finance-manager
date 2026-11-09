
using FinanceManager.Application.DTOs.Dashboard;

namespace FinanceManager.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(
        Guid userId,
        CancellationToken ct = default);

    Task<CashFlowForecastResponse> GetForecastAsync(
        Guid userId,
        int horizonDays,
        CancellationToken ct = default);
}
