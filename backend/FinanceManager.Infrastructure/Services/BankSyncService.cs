using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

/// <summary>
/// Service for syncing bank transactions
/// TODO: Integrate with Plaid SDK for actual transaction retrieval
/// </summary>
public class BankSyncService : IBankSyncService
{
    private readonly FinanceDbContext _dbContext;
    private readonly ILogger<BankSyncService> _logger;

    public BankSyncService(
        FinanceDbContext dbContext,
        ILogger<BankSyncService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<BankSyncLog> SyncTransactionsAsync(
        Guid bankConnectionId,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var syncLog = new BankSyncLog
        {
            BankConnectionId = bankConnectionId,
            StartedAtUtc = DateTime.UtcNow,
            Status = "Running",
            SyncFromDateUtc = fromDate,
            SyncToDateUtc = toDate
        };

        try
        {
            var connection = await _dbContext.BankConnections
                .FirstOrDefaultAsync(bc => bc.Id == bankConnectionId);

            if (connection == null)
            {
                syncLog.Status = "Failed";
                syncLog.ErrorMessage = "Bank connection not found";
                syncLog.CompletedAtUtc = DateTime.UtcNow;
                _dbContext.BankSyncLogs.Add(syncLog);
                await _dbContext.SaveChangesAsync();
                return syncLog;
            }

            _logger.LogInformation(
                "Starting sync for connection {ConnectionId}, provider {Provider}",
                bankConnectionId,
                connection.BankProvider);

            // TODO: Call Plaid/Finicity SDK to fetch transactions
            // For now, just log that we're attempting
            
            syncLog.TransactionsRetrieved = 0;
            syncLog.TransactionsImported = 0;
            syncLog.DuplicatesSkipped = 0;
            syncLog.ErrorCount = 0;
            syncLog.Status = "Success";
            syncLog.CompletedAtUtc = DateTime.UtcNow;

            connection.LastSyncAtUtc = DateTime.UtcNow;
            connection.UpdatedAtUtc = DateTime.UtcNow;

            _dbContext.BankSyncLogs.Add(syncLog);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Completed sync for connection {ConnectionId}: {Imported} imported, {Duplicates} duplicates",
                bankConnectionId,
                syncLog.TransactionsImported,
                syncLog.DuplicatesSkipped);

            return syncLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for connection {ConnectionId}", bankConnectionId);
            
            syncLog.Status = "Failed";
            syncLog.ErrorMessage = ex.Message;
            syncLog.DetailedLog = ex.StackTrace;
            syncLog.CompletedAtUtc = DateTime.UtcNow;

            _dbContext.BankSyncLogs.Add(syncLog);
            await _dbContext.SaveChangesAsync();

            return syncLog;
        }
    }

    public async Task<IEnumerable<BankSyncLog>> SyncAllConnectionsAsync()
    {
        // Called by background service
        var syncLogs = new List<BankSyncLog>();

        var connections = await _dbContext.BankConnections
            .Where(bc => !bc.IsDeleted && bc.Status == 1) // Only Connected
            .ToListAsync();

        _logger.LogInformation("Starting batch sync of {Count} connections", connections.Count);

        foreach (var connection in connections)
        {
            try
            {
                var syncLog = await SyncTransactionsAsync(connection.Id);
                syncLogs.Add(syncLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync batch failed for connection {ConnectionId}", connection.Id);
            }
        }

        return syncLogs;
    }

    public async Task<IEnumerable<BankSyncLog>> GetSyncHistoryAsync(
        Guid bankConnectionId,
        int pageNumber = 1,
        int pageSize = 10)
    {
        var syncLogs = await _dbContext.BankSyncLogs
            .Where(sl => sl.BankConnectionId == bankConnectionId)
            .OrderByDescending(sl => sl.StartedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return syncLogs;
    }
}
