namespace FinanceManager.Domain.Entities;

public sealed class SavingsGoalContribution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid SavingsGoalId { get; set; }

    public decimal Amount { get; set; }

    public DateTime ContributionDateUtc { get; set; } = DateTime.UtcNow;

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
