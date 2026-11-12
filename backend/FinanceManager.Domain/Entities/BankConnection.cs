namespace FinanceManager.Domain.Entities;

/// <summary>
/// Represents a connection to a user's bank account via Plaid/Finicity
/// </summary>
public class BankConnection
{
    /// <summary>Unique identifier for this bank connection</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User who owns this bank connection</summary>
    public Guid UserId { get; set; }

    /// <summary>Financial account this connection syncs to (optional, can have multiple accounts per connection)</summary>
    public Guid? FinancialAccountId { get; set; }

    /// <summary>Plaid: access_token OR Finicity: customerId</summary>
    public string? PlaidAccessToken { get; set; }

    /// <summary>Plaid: item_id for this institution connection</summary>
    public string? PlaidItemId { get; set; }

    /// <summary>Finicity: institution name (e.g., "TD Bank")</summary>
    public string? FinicityInstitutionName { get; set; }

    /// <summary>Finicity: customerId</summary>
    public string? FinicityCustomerId { get; set; }

    /// <summary>Bank provider: "Plaid" or "Finicity"</summary>
    public string BankProvider { get; set; } = "Plaid";

    /// <summary>Institution name (e.g., "Royal Bank of Canada")</summary>
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>Institution code/ID from bank provider</summary>
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>Current status of the connection</summary>
    public int Status { get; set; } = 0; // BankConnectionStatus.Pending

    /// <summary>When the connection was established</summary>
    public DateTime ConnectedAtUtc { get; set; }

    /// <summary>Last successful sync</summary>
    public DateTime? LastSyncAtUtc { get; set; }

    /// <summary>Plaid /transactions/sync cursor; null means sync from the beginning</summary>
    public string? TransactionsCursor { get; set; }

    /// <summary>When the connection will expire (Plaid links expire annually)</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Error message if connection failed</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>When this record was created</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When this record was last updated</summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Soft delete flag</summary>
    public bool IsDeleted { get; set; } = false;

    // When the owner's plan stopped including bank sync. The connection is
    // removed at Plaid after a grace period; cleared if they upgrade again.
    public DateTime? PlanLapsedAtUtc { get; set; }

    /// <summary>Navigation property: synced bank transactions</summary>
    public virtual ICollection<BankTransaction> BankTransactions { get; set; } = [];

    /// <summary>Navigation property: sync logs</summary>
    public virtual ICollection<BankSyncLog> SyncLogs { get; set; } = [];
}
