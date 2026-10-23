
using FinanceManager.Application.DTOs.Dashboard;

namespace FinanceManager.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(
        Guid userId,
        CancellationToken ct = default);
}
