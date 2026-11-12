using FinanceManager.Domain.Enums;

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
    DateTime UpdatedAtUtc,
    Guid? HouseholdId,
    bool CanEdit,
    SavingsGoalType GoalType,
    int? EmergencyFundMonths,
    decimal? AverageMonthlyExpenses,
    Guid? LinkedAccountId,
    string? LinkedAccountName,
    decimal? AutoContributionAmount,
    RecurrenceFrequency? AutoContributionFrequency,
    DateTime? AutoContributionStartUtc,
    DateTime? NextAutoContributionUtc,
    bool NotificationsEnabled,
    SavingsGoalStatus Status,
    decimal MonthlyPace,
    decimal? MonthlyContributionNeeded,
    DateTime? ProjectedCompletionDateUtc);
