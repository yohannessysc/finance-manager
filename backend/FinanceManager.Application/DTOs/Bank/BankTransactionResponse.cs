namespace FinanceManager.Application.DTOs.Bank;

/// <summary>
/// Bank transaction from sync
/// </summary>
public class BankTransactionResponse
{
    /// <summary>Unique identifier for this bank transaction record</summary>
    public Guid Id { get; set; }

    /// <summary>Bank's transaction ID</summary>
    public string BankTransactionId { get; set; } = string.Empty;

    /// <summary>Transaction date</summary>
    public DateTime TransactionDateUtc { get; set; }

    /// <summary>Amount (positive for income, negative for expenses)</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency code</summary>
    public string Currency { get; set; } = "CAD";

    /// <summary>Transaction description</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Merchant name if available</summary>
    public string? Merchant { get; set; }

    /// <summary>Status: "Pending", "Posted", "Reversed", "Categorized"</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Auto-matched category if available</summary>
    public string? AutoMatchedCategory { get; set; }

    /// <summary>Confidence score of auto-match (0-100)</summary>
    public int? AutoMatchConfidence { get; set; }

    /// <summary>Associated local transaction ID if categorized</summary>
    public Guid? FinancialTransactionId { get; set; }

    /// <summary>When this transaction was imported</summary>
    public DateTime ImportedAtUtc { get; set; }
}
