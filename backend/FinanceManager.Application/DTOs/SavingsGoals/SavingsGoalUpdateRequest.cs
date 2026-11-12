using FinanceManager.Domain.Enums;

namespace FinanceManager.Application.DTOs.SavingsGoals;

// Full replacement: omitted optional settings are cleared.
public sealed record SavingsGoalUpdateRequest(
    string Name,
    decimal TargetAmount,
    DateTime? TargetDateUtc,
    int? EmergencyFundMonths = null,
    Guid? LinkedAccountId = null,
    decimal? AutoContributionAmount = null,
    RecurrenceFrequency? AutoContributionFrequency = null,
    DateTime? AutoContributionStartUtc = null,
    bool NotificationsEnabled = true);
