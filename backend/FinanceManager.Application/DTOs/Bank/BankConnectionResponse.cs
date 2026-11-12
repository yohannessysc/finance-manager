namespace FinanceManager.Application.DTOs.Bank;

/// <summary>
/// Response representing a bank connection
/// </summary>
public class BankConnectionResponse
{
    /// <summary>Unique identifier for this bank connection</summary>
    public Guid Id { get; set; }

    /// <summary>Institution name (e.g., "Royal Bank of Canada")</summary>
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>Bank provider: "Plaid" or "Finicity"</summary>
    public string BankProvider { get; set; } = string.Empty;

    /// <summary>Status: "Pending", "Connected", "ExpiredLink", "Disconnected", "Failed"</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the connection was established</summary>
    public DateTime ConnectedAtUtc { get; set; }

    /// <summary>Last successful sync time</summary>
    public DateTime? LastSyncAtUtc { get; set; }

    /// <summary>When the connection will expire</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Error message if connection failed</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Number of transactions synced</summary>
    public int TransactionCount { get; set; }

    /// <summary>Financial account ID this is linked to (if any)</summary>
    public Guid? FinancialAccountId { get; set; }

    /// <summary>When the connection will be removed because the plan no longer includes bank sync</summary>
    public DateTime? RemovalScheduledAtUtc { get; set; }
}
