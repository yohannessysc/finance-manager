namespace FinanceManager.Domain.Entities;

public class FinancialAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AccountType { get; set; } = string.Empty;

    public string Currency { get; set; } = "CAD";

    public decimal OpeningBalance { get; set; }

    /// <summary>Bank connection this account is synced from (null = manual account)</summary>
    public Guid? BankConnectionId { get; set; }

    /// <summary>Provider's account id (Plaid account_id) for synced accounts</summary>
    public string? ExternalAccountId { get; set; }

    /// <summary>Last digits of the bank account number, for display</summary>
    public string? Mask { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}