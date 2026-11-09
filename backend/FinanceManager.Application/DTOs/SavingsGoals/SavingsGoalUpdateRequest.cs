namespace FinanceManager.Application.DTOs.SavingsGoals;

public sealed record SavingsGoalUpdateRequest(
    string Name,
    decimal TargetAmount,
    DateTime? TargetDateUtc);
