using FinanceManager.Application.DTOs.Reports;

namespace FinanceManager.Application.Interfaces;

public interface IReportService
{
    // Months are inclusive. Currency null picks the one with the most activity.
    Task<CategoryReportResponse> GetCategoryReportAsync(
        Guid userId,
        DateOnly fromMonth,
        DateOnly toMonth,
        string? currency,
        CancellationToken ct = default);
}
