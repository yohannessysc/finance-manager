namespace FinanceManager.Application.DTOs.SavingsGoals;

public sealed record SavingsGoalResponse(
    Guid Id,
    string Name,
    string Currency,
    decimal TargetAmount,
    decimal AmountSaved,
    decimal Remaining,
    decimal ProgressPercent,
    DateTime? TargetDateUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
