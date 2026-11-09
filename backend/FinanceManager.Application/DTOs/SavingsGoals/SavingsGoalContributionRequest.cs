namespace FinanceManager.Application.DTOs.SavingsGoals;

public sealed record SavingsGoalContributionRequest(
    decimal Amount,
    DateTime ContributionDateUtc,
    string? Note);
