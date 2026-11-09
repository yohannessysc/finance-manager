namespace FinanceManager.Domain.Entities;

/// <summary>
/// Audit log for bank synchronization operations
/// </summary>
public class BankSyncLog
{
    /// <summary>Unique identifier for this sync log entry</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Bank connection this sync was for</summary>
    public Guid BankConnectionId { get; set; }

    /// <summary>When the sync started</summary>
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When the sync completed</summary>
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Status: "Running", "Success", "PartialSuccess", "Failed"</summary>
    public string Status { get; set; } = "Running";

    /// <summary>Number of transactions retrieved from bank</summary>
    public int TransactionsRetrieved { get; set; }

    /// <summary>Number of new transactions imported</summary>
    public int TransactionsImported { get; set; }

    /// <summary>Number of duplicate transactions skipped</summary>
    public int DuplicatesSkipped { get; set; }

    /// <summary>Number of transactions with errors</summary>
    public int ErrorCount { get; set; }

    /// <summary>Error message if sync failed</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Date range queried from bank (from date)</summary>
    public DateTime? SyncFromDateUtc { get; set; }

    /// <summary>Date range queried from bank (to date)</summary>
    public DateTime? SyncToDateUtc { get; set; }

    /// <summary>Detailed error log for debugging</summary>
    public string? DetailedLog { get; set; }

    /// <summary>Navigation property: bank connection</summary>
    public virtual BankConnection? BankConnection { get; set; }
}
