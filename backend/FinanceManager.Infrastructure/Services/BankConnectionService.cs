using FinanceManager.Application.DTOs.Bank;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services.Plaid;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

/// <summary>
/// Service for managing bank connections using Plaid SDK
/// </summary>
public class BankConnectionService : IBankConnectionService
{
    private readonly FinanceDbContext _dbContext;
    private readonly IPlaidService _plaidService;
    private readonly ILogger<BankConnectionService> _logger;
    private readonly IBankTokenProtector _tokenProtector;
    private readonly IEntitlementService _entitlements;

    // How long a downgraded user's banks stay linked (paused) before they
    // are removed at Plaid. Upgrading again within it resumes syncing.
    public static readonly TimeSpan LapsedConnectionGracePeriod = TimeSpan.FromDays(30);

    public BankConnectionService(
        FinanceDbContext dbContext,
        IPlaidService plaidService,
        IBankTokenProtector tokenProtector,
        IEntitlementService entitlements,
        ILogger<BankConnectionService> logger)
    {
        _tokenProtector = tokenProtector;
        _entitlements = entitlements;
        _dbContext = dbContext;
        _plaidService = plaidService;
        _logger = logger;
    }

    public async Task<PlaidLinkTokenResponse> CreatePlaidLinkTokenAsync(
        Guid userId,
        CreatePlaidLinkRequest request)
    {
        _logger.LogInformation("Creating Plaid Link token for user {UserId}", userId);

        try
        {
            var result = await _plaidService.CreateLinkTokenAsync(
                userId.ToString(),
                request.UserEmail,
                request.UserFullName,
                countryCodes: new[] { "CA" }); // Canadian banks

            return new PlaidLinkTokenResponse
            {
                LinkToken = result.LinkToken,
                ExpiresAtUtc = result.ExpiresAtUtc,
                RequestId = result.RequestId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Plaid Link token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<BankConnectionResponse> ExchangePlaidTokenAsync(
        Guid userId,
        ExchangePlaidTokenRequest request)
    {
        _logger.LogInformation(
            "Exchanging Plaid token for user {UserId}, institution {Institution}",
            userId,
            request.Metadata?.InstitutionName);

        try
        {
            // Exchange public token for access token
            var tokenResult = await _plaidService.ExchangePublicTokenAsync(request.PublicToken);

            // Save connection to database
            var connection = new BankConnection
            {
                UserId = userId,
                FinancialAccountId = request.FinancialAccountId,
                BankProvider = "Plaid",
                InstitutionName = request.Metadata?.InstitutionName ?? "Unknown",
                InstitutionId = request.Metadata?.InstitutionId ?? "",
                PlaidItemId = tokenResult.ItemId,
                PlaidAccessToken = _tokenProtector.Protect(tokenResult.AccessToken),
                Status = 1, // Connected
                ConnectedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddYears(1) // Plaid items expire after 1 year
            };

            _dbContext.BankConnections.Add(connection);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully exchanged Plaid token and saved connection {ConnectionId} for user {UserId}",
                connection.Id,
                userId);

            return MapToResponse(connection, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange Plaid token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<IEnumerable<BankConnectionResponse>> GetUserConnectionsAsync(Guid userId)
    {
        var connections = await _dbContext.BankConnections
            .Where(bc => bc.UserId == userId && !bc.IsDeleted)
            .ToListAsync();

        var responses = new List<BankConnectionResponse>();
        foreach (var connection in connections)
        {
            var transactionCount = await _dbContext.BankTransactions
                .CountAsync(bt => bt.BankConnectionId == connection.Id);

            responses.Add(MapToResponse(connection, transactionCount));
        }

        return responses;
    }

    public async Task<BankConnectionResponse?> GetConnectionAsync(Guid connectionId, Guid userId)
    {
        var connection = await _dbContext.BankConnections
            .FirstOrDefaultAsync(bc => bc.Id == connectionId && bc.UserId == userId && !bc.IsDeleted);

        if (connection == null)
            return null;

        var transactionCount = await _dbContext.BankTransactions
            .CountAsync(bt => bt.BankConnectionId == connectionId);

        return MapToResponse(connection, transactionCount);
    }

    public async Task<PlaidLinkTokenResponse?> CreateReconnectLinkTokenAsync(
        Guid connectionId,
        Guid userId)
    {
        var connection = await _dbContext.BankConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(bc =>
                bc.Id == connectionId &&
                bc.UserId == userId &&
                !bc.IsDeleted &&
                bc.PlaidAccessToken != null);

        if (connection is null)
            return null;

        var result = await _plaidService.CreateLinkTokenAsync(
            userId.ToString(),
            countryCodes: new[] { "CA" },
            accessToken: _tokenProtector.Unprotect(connection.PlaidAccessToken!));

        return new PlaidLinkTokenResponse
        {
            LinkToken = result.LinkToken,
            ExpiresAtUtc = result.ExpiresAtUtc,
            RequestId = result.RequestId
        };
    }

    public async Task<bool> MarkReconnectedAsync(Guid connectionId, Guid userId)
    {
        var connection = await _dbContext.BankConnections
            .FirstOrDefaultAsync(bc =>
                bc.Id == connectionId &&
                bc.UserId == userId &&
                !bc.IsDeleted);

        if (connection is null)
            return false;

        connection.Status = (int)BankConnectionStatus.Connected;
        connection.ErrorMessage = null;
        connection.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DisconnectAsync(Guid connectionId, Guid userId)
    {
        var connection = await _dbContext.BankConnections
            .FirstOrDefaultAsync(bc => bc.Id == connectionId && bc.UserId == userId);

        if (connection == null)
            return false;

        // Revoke access at Plaid so the item stops being billed / refreshed.
        // Imported accounts and transactions are kept as the user's history.
        if (!string.IsNullOrEmpty(connection.PlaidAccessToken))
        {
            try
            {
                await _plaidService.RemoveItemAsync(
                    _tokenProtector.Unprotect(connection.PlaidAccessToken));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plaid item removal failed for connection {ConnectionId}", connectionId);
            }
            connection.PlaidAccessToken = null;
        }

        connection.IsDeleted = true;
        connection.Status = 3; // Disconnected
        connection.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Bank connection {ConnectionId} disconnected for user {UserId}",
            connectionId,
            userId);

        return true;
    }

    public async Task<int> RemoveLapsedConnectionsAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var connections = await _dbContext.BankConnections
            .Where(bc => !bc.IsDeleted)
            .ToListAsync(ct);

        var removed = 0;
        foreach (var group in connections.GroupBy(bc => bc.UserId))
        {
            ct.ThrowIfCancellationRequested();
            var entitled = await _entitlements.GetPlanAsync(group.Key, ct) >= SubscriptionPlan.Bank;

            foreach (var connection in group)
            {
                if (entitled)
                {
                    connection.PlanLapsedAtUtc = null;
                }
                else if (connection.PlanLapsedAtUtc is null)
                {
                    connection.PlanLapsedAtUtc = nowUtc;
                }
                else if (connection.PlanLapsedAtUtc + LapsedConnectionGracePeriod <= nowUtc)
                {
                    await DisconnectAsync(connection.Id, connection.UserId);
                    removed++;
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        if (removed > 0)
        {
            _logger.LogInformation(
                "Removed {Count} bank connections whose plan no longer includes bank sync.",
                removed);
        }

        return removed;
    }

    public async Task<IEnumerable<BankTransactionResponse>> GetConnectionTransactionsAsync(
        Guid connectionId,
        Guid userId,
        int pageNumber = 1,
        int pageSize = 50)
    {
        // Verify user owns this connection
        var connection = await _dbContext.BankConnections
            .FirstOrDefaultAsync(bc => bc.Id == connectionId && bc.UserId == userId && !bc.IsDeleted);

        if (connection == null)
            return Enumerable.Empty<BankTransactionResponse>();

        var transactions = await _dbContext.BankTransactions
            .Where(bt => bt.BankConnectionId == connectionId)
            .OrderByDescending(bt => bt.TransactionDateUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return transactions.Select(bt => new BankTransactionResponse
        {
            Id = bt.Id,
            BankTransactionId = bt.BankTransactionId,
            TransactionDateUtc = bt.TransactionDateUtc,
            Amount = bt.Amount,
            Currency = bt.Currency,
            Description = bt.Description,
            Merchant = bt.Merchant,
            Status = bt.Status.ToString(),
            AutoMatchedCategory = bt.AutoMatchedCategory,
            AutoMatchConfidence = bt.AutoMatchConfidence,
            FinancialTransactionId = bt.FinancialTransactionId,
            ImportedAtUtc = bt.ImportedAtUtc
        });
    }

    public async Task<BankSyncLog?> SyncConnectionAsync(Guid connectionId, Guid userId)
    {
        // Verify user owns this connection
        var connection = await _dbContext.BankConnections
            .FirstOrDefaultAsync(bc => bc.Id == connectionId && bc.UserId == userId && !bc.IsDeleted);

        if (connection == null)
            return null;

        // TODO: Trigger sync service
        _logger.LogInformation(
            "Sync requested for connection {ConnectionId} by user {UserId}",
            connectionId,
            userId);

        // Create a placeholder sync log
        var syncLog = new BankSyncLog
        {
            BankConnectionId = connectionId,
            Status = "Running",
            TransactionsRetrieved = 0,
            TransactionsImported = 0,
            DuplicatesSkipped = 0,
            ErrorCount = 0
        };

        _dbContext.BankSyncLogs.Add(syncLog);
        await _dbContext.SaveChangesAsync();

        return syncLog;
    }

    private static BankConnectionResponse MapToResponse(BankConnection connection, int transactionCount)
    {
        return new BankConnectionResponse
        {
            Id = connection.Id,
            InstitutionName = connection.InstitutionName,
            BankProvider = connection.BankProvider,
            Status = GetStatusString(connection.Status),
            ConnectedAtUtc = connection.ConnectedAtUtc,
            LastSyncAtUtc = connection.LastSyncAtUtc,
            ExpiresAtUtc = connection.ExpiresAtUtc,
            ErrorMessage = connection.ErrorMessage,
            TransactionCount = transactionCount,
            FinancialAccountId = connection.FinancialAccountId,
            RemovalScheduledAtUtc = connection.PlanLapsedAtUtc + LapsedConnectionGracePeriod
        };
    }

    private static string GetStatusString(int status)
    {
        return status switch
        {
            0 => "Pending",
            1 => "Connected",
            2 => "ExpiredLink",
            3 => "Disconnected",
            4 => "Failed",
            _ => "Unknown"
        };
    }
}
