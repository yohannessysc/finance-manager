namespace FinanceManager.Application.DTOs.SavingsGoals;

public sealed record SavingsGoalUpsertRequest(
    string Name,
    string Currency,
    decimal TargetAmount,
    DateTime? TargetDateUtc);
