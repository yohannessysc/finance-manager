using System.Security.Cryptography;
using System.Text;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services.Plaid;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

/// <summary>
/// Service for syncing bank transactions from Plaid
/// Handles deduplication, auto-categorization, and sync logging
/// </summary>
public class BankSyncService : IBankSyncService
{
    private readonly FinanceDbContext _dbContext;
    private readonly IPlaidService _plaidService;
    private readonly ICategoryRuleService _categoryRuleService;
    private readonly ILogger<BankSyncService> _logger;

    public BankSyncService(
        FinanceDbContext dbContext,
        IPlaidService plaidService,
        ICategoryRuleService categoryRuleService,
        ILogger<BankSyncService> logger)
    {
        _dbContext = dbContext;
        _plaidService = plaidService;
        _categoryRuleService = categoryRuleService;
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

            // Set default date range (last 90 days if not specified)
            var startDate = fromDate ?? DateTime.UtcNow.AddDays(-90);
            var endDate = toDate ?? DateTime.UtcNow;

            // Fetch transactions from Plaid
            var plaidResult = await _plaidService.GetTransactionsAsync(
                connection.PlaidAccessToken ?? "",
                startDate,
                endDate);

            syncLog.TransactionsRetrieved = plaidResult.Transactions.Count;
            
            _logger.LogInformation(
                "Retrieved {Count} transactions from Plaid for connection {ConnectionId}",
                syncLog.TransactionsRetrieved,
                bankConnectionId);

            // Process and deduplicate transactions
            var imported = 0;
            var duplicates = 0;
            var errors = 0;

            foreach (var plaidTxn in plaidResult.Transactions)
            {
                try
                {
                    // Calculate hash for duplicate detection
                    var transactionHash = CalculateTransactionHash(
                        plaidTxn.Date,
                        plaidTxn.Amount,
                        plaidTxn.Name,
                        plaidTxn.MerchantName);

                    // Check if transaction already exists
                    var existingTxn = await _dbContext.BankTransactions
                        .FirstOrDefaultAsync(bt =>
                            bt.BankConnectionId == bankConnectionId &&
                            bt.TransactionHash == transactionHash);

                    if (existingTxn != null)
                    {
                        duplicates++;
                        continue;
                    }

                    // Create new bank transaction
                    var bankTxn = new BankTransaction
                    {
                        BankConnectionId = bankConnectionId,
                        BankTransactionId = plaidTxn.TransactionId,
                        TransactionDateUtc = plaidTxn.Date,
                        Amount = plaidTxn.Amount,
                        Currency = plaidTxn.Currency,
                        Description = plaidTxn.MerchantName ?? plaidTxn.Name,
                        Merchant = plaidTxn.MerchantName,
                        Status = 1, // Posted
                        TransactionHash = transactionHash,
                        ImportedAtUtc = DateTime.UtcNow
                    };

                    // Auto-categorize using rules
                    if (_categoryRuleService != null)
                    {
                        var categoryResult = await _categoryRuleService.EvaluateTransactionAsync(
                            connection.UserId,
                            plaidTxn.Name,
                            plaidTxn.MerchantName,
                            plaidTxn.Amount);

                        if (categoryResult.CategoryId.HasValue)
                        {
                            bankTxn.AutoMatchedCategory = categoryResult.RuleName;
                            bankTxn.AutoMatchConfidence = categoryResult.Confidence;
                        }
                    }

                    _dbContext.BankTransactions.Add(bankTxn);
                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error processing transaction {TransactionId} for connection {ConnectionId}",
                        plaidTxn.TransactionId,
                        bankConnectionId);
                    errors++;
                }
            }

            // Batch save all transactions
            await _dbContext.SaveChangesAsync();

            syncLog.TransactionsImported = imported;
            syncLog.DuplicatesSkipped = duplicates;
            syncLog.ErrorCount = errors;
            syncLog.Status = "Success";
            syncLog.CompletedAtUtc = DateTime.UtcNow;

            // Update connection's last sync time
            connection.LastSyncAtUtc = DateTime.UtcNow;
            connection.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _dbContext.BankSyncLogs.Add(syncLog);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Completed sync for connection {ConnectionId}: {Imported} imported, {Duplicates} duplicates, {Errors} errors",
                bankConnectionId,
                imported,
                duplicates,
                errors);

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

    /// <summary>
    /// Calculate SHA256 hash of transaction for duplicate detection
    /// Hash includes date, amount, description, and merchant to uniquely identify a transaction
    /// </summary>
    private string CalculateTransactionHash(DateTime date, decimal amount, string description, string? merchant)
    {
        var input = $"{date:yyyy-MM-dd}|{amount:F2}|{description}|{merchant ?? ""}";
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }
}

