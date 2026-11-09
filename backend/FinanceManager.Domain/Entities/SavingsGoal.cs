namespace FinanceManager.Domain.Entities;

public sealed class SavingsGoal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Currency { get; set; } = "CAD";

    public decimal TargetAmount { get; set; }

    public DateTime? TargetDateUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
