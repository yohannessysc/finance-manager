namespace FinanceManager.Domain.Entities;

/// <summary>
/// Represents a transaction from a connected bank account
/// </summary>
public class BankTransaction
{
    /// <summary>Unique identifier for this bank transaction record</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Bank connection this transaction came from</summary>
    public Guid BankConnectionId { get; set; }

    /// <summary>The corresponding local FinancialTransaction if categorized</summary>
    public Guid? FinancialTransactionId { get; set; }

    /// <summary>Bank's transaction ID (unique per institution)</summary>
    public string BankTransactionId { get; set; } = string.Empty;

    /// <summary>Transaction date from bank</summary>
    public DateTime TransactionDateUtc { get; set; }

    /// <summary>Amount (positive for income, negative for expenses)</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency code (e.g., "CAD")</summary>
    public string Currency { get; set; } = "CAD";

    /// <summary>Transaction description from bank</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Merchant name if available</summary>
    public string? Merchant { get; set; }

    /// <summary>Bank account type (e.g., "CHECKING", "SAVINGS")</summary>
    public string AccountType { get; set; } = string.Empty;

    /// <summary>Masked account number (last 4 digits)</summary>
    public string? MaskedAccountNumber { get; set; }

    /// <summary>Status: Pending, Posted, Reversed, or Categorized</summary>
    public int Status { get; set; } = 1; // BankTransactionStatus.Posted

    /// <summary>Hash of transaction for duplicate detection</summary>
    public string TransactionHash { get; set; } = string.Empty;

    /// <summary>Category auto-matched by rules engine</summary>
    public string? AutoMatchedCategory { get; set; }

    /// <summary>Confidence score of auto-match (0-100)</summary>
    public int? AutoMatchConfidence { get; set; }

    /// <summary>When this transaction was imported</summary>
    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation property: bank connection</summary>
    public virtual BankConnection? BankConnection { get; set; }

    /// <summary>Navigation property: local financial transaction if categorized</summary>
    public virtual FinancialTransaction? FinancialTransaction { get; set; }
}
