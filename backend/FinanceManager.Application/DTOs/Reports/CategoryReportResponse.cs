using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.Reports;

// Income and spending by category and month for one currency.
// Months are "yyyy-MM" in the user's time zone; every ByMonth array
// lines up with Months.
public sealed record CategoryReportResponse(
    string Currency,
    IReadOnlyList<string> Currencies,
    IReadOnlyList<string> Months,
    IReadOnlyList<decimal> IncomeByMonth,
    IReadOnlyList<decimal> ExpensesByMonth,
    IReadOnlyList<CategoryReportRow> Categories);

public sealed record CategoryReportRow(
    Guid? CategoryId,
    string Name,
    TransactionType Type,
    decimal Total,
    IReadOnlyList<decimal> ByMonth,
    // Subcategories, plus a "General" row for amounts on the parent itself.
    IReadOnlyList<CategoryReportRow> Children);
