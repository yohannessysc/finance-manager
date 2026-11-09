using FinanceManager.Application.DTOs.Bank;
using FinanceManager.Domain.Entities;

namespace FinanceManager.Application.Interfaces;

/// <summary>
/// Service for managing bank connections and Plaid/Finicity integration
/// </summary>
public interface IBankConnectionService
{
    /// <summary>
    /// Create a Plaid Link token for initiating bank account connection
    /// </summary>
    Task<PlaidLinkTokenResponse> CreatePlaidLinkTokenAsync(
        Guid userId,
        CreatePlaidLinkRequest request);

    /// <summary>
    /// Exchange Plaid public token for access token and store connection
    /// </summary>
    Task<BankConnectionResponse> ExchangePlaidTokenAsync(
        Guid userId,
        ExchangePlaidTokenRequest request);

    /// <summary>
    /// Get all bank connections for a user
    /// </summary>
    Task<IEnumerable<BankConnectionResponse>> GetUserConnectionsAsync(Guid userId);

    /// <summary>
    /// Get a specific bank connection
    /// </summary>
    Task<BankConnectionResponse?> GetConnectionAsync(Guid connectionId, Guid userId);

    /// <summary>
    /// Disconnect a bank connection (mark as deleted)
    /// </summary>
    Task<bool> DisconnectAsync(Guid connectionId, Guid userId);

    /// <summary>
    /// Get all bank transactions for a connection
    /// </summary>
    Task<IEnumerable<BankTransactionResponse>> GetConnectionTransactionsAsync(
        Guid connectionId,
        Guid userId,
        int pageNumber = 1,
        int pageSize = 50);

    /// <summary>
    /// Manually trigger a sync for a specific connection
    /// </summary>
    Task<BankSyncLog?> SyncConnectionAsync(Guid connectionId, Guid userId);
}
