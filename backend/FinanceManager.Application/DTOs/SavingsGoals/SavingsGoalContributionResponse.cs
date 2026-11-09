namespace FinanceManager.Application.DTOs.SavingsGoals;

public sealed record SavingsGoalContributionResponse(
    Guid Id,
    Guid SavingsGoalId,
    decimal Amount,
    DateTime ContributionDateUtc,
    string? Note,
    DateTime CreatedAtUtc);
