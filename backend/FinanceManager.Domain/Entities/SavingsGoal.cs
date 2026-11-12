using FinanceManager.Domain.Enums;

namespace FinanceManager.Domain.Entities;

public sealed class SavingsGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    // Shared with every member of this household when set.
    public Guid? HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Currency { get; set; } = "CAD";

    public SavingsGoalType GoalType { get; set; } = SavingsGoalType.Custom;

    // Emergency funds target this many months of average spending.
    // TargetAmount is the fallback until there is spending history.
    public int? EmergencyFundMonths { get; set; }

    public decimal TargetAmount { get; set; }

    public DateTime? TargetDateUtc { get; set; }

    // When set, progress is the account's balance instead of
    // the sum of manual contributions.
    public Guid? LinkedAccountId { get; set; }

    public decimal? AutoContributionAmount { get; set; }

    public RecurrenceFrequency? AutoContributionFrequency { get; set; }

    // Anchor for the schedule so month-end dates do not drift.
    public DateTime? AutoContributionStartUtc { get; set; }

    // Null when there is no schedule or the goal has been reached.
    public DateTime? NextAutoContributionUtc { get; set; }

    public bool NotificationsEnabled { get; set; } = true;

    // Highest progress milestone (0, 25, 50, 75, 100) already notified.
    public int LastMilestoneNotified { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
