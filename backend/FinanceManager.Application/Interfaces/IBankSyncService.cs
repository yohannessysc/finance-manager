using FinanceManager.Domain.Entities;

namespace FinanceManager.Application.Interfaces;

/// <summary>
/// Service for syncing transactions from banks and handling auto-categorization
/// </summary>
public interface IBankSyncService
{
    /// <summary>
    /// Sync transactions from a bank connection
    /// </summary>
    Task<BankSyncLog> SyncTransactionsAsync(
        Guid bankConnectionId,
        DateTime? fromDate = null,
        DateTime? toDate = null);

    /// <summary>
    /// Sync all pending connections (called by background service)
    /// </summary>
    Task<IEnumerable<BankSyncLog>> SyncAllConnectionsAsync();

    /// <summary>
    /// Get sync history for a connection
    /// </summary>
    Task<IEnumerable<BankSyncLog>> GetSyncHistoryAsync(
        Guid bankConnectionId,
        int pageNumber = 1,
        int pageSize = 10);
}
