namespace FinanceManager.Application.DTOs.Budgets;

public sealed record BudgetUpsertRequest(
    Guid CategoryId,
    string Currency,
    decimal MonthlyLimit,
    Guid? HouseholdId = null);
