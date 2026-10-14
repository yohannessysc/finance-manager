namespace FinanceManager.Domain.Entities;

public class FinancialAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccountType { get; set; } = string.Empty;

    public string Currency { get; set; } = "CAD";

    public decimal OpeningBalance { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}