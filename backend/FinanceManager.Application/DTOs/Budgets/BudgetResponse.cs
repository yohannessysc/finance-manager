namespace FinanceManager.Application.DTOs.Budgets;

public sealed record BudgetResponse(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Currency,
    decimal MonthlyLimit,
    decimal AmountSpent,
    decimal Remaining,
    decimal PercentSpent,
    DateTime PeriodStartUtc,
    DateTime PeriodEndUtc,
    DateOnly PeriodStartDate,
    DateOnly PeriodEndDate,
    Guid? HouseholdId = null,
    bool CanEdit = true);
