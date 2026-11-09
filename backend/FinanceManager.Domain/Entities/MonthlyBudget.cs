namespace FinanceManager.Domain.Entities;

public sealed class MonthlyBudget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }

    public Guid CategoryId { get; set; }

    public string Currency { get; set; } = "CAD";

    public decimal MonthlyLimit { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
