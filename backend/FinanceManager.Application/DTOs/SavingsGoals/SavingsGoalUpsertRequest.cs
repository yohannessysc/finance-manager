using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.SavingsGoals;

public sealed record SavingsGoalUpsertRequest(
    string Name,
    string Currency,
    decimal TargetAmount,
    DateTime? TargetDateUtc,
    Guid? HouseholdId = null,
    SavingsGoalType GoalType = SavingsGoalType.Custom,
    int? EmergencyFundMonths = null,
    Guid? LinkedAccountId = null,
    decimal? AutoContributionAmount = null,
    RecurrenceFrequency? AutoContributionFrequency = null,
    DateTime? AutoContributionStartUtc = null,
    bool NotificationsEnabled = true);
