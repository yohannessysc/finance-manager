using System.Security.Cryptography;
using System.Text;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services.Plaid;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

/// <summary>
/// Pulls data from Plaid into the app's own tables, so bank activity shows up
/// everywhere manual data does (accounts, transactions, dashboard, budgets):
///   1. Each bank account becomes a FinancialAccount whose balance matches the bank.
///   2. Posted bank transactions become FinancialTransactions (Income/Expense), categorized.
///   3. Plaid-detected recurring income/bills become RecurringTransactions that are
///      informational only (ExternalStreamId set) and never auto-posted.
/// </summary>
public class BankSyncService : IBankSyncService
{
    private const string FingerprintPrefix = "plaid:";

    private readonly FinanceDbContext _dbContext;
    private readonly IPlaidService _plaidService;
    private readonly ICategoryRuleService _categoryRuleService;
    private readonly ILogger<BankSyncService> _logger;
    private readonly IBankTokenProtector _tokenProtector;
    private readonly IEntitlementService _entitlements;

    public BankSyncService(
        FinanceDbContext dbContext,
        IPlaidService plaidService,
        ICategoryRuleService categoryRuleService,
        IBankTokenProtector tokenProtector,
        IEntitlementService entitlements,
        ILogger<BankSyncService> logger)
    {
        _tokenProtector = tokenProtector;
        _entitlements = entitlements;
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
                .FirstOrDefaultAsync(bc => bc.Id == bankConnectionId && !bc.IsDeleted);

            if (connection == null || string.IsNullOrEmpty(connection.PlaidAccessToken))
            {
                return await FinishAsync(syncLog, "Failed", "Bank connection not found");
            }

            var accessToken = _tokenProtector.Unprotect(connection.PlaidAccessToken);
            var categories = new CategoryResolver(_dbContext, connection.UserId);

            // 1. Accounts
            var plaidAccounts = await _plaidService.GetAccountsAsync(accessToken);
            var accountMap = await UpsertAccountsAsync(connection, plaidAccounts.Accounts);

            // 2. Transactions (incremental)
            var sync = await _plaidService.SyncTransactionsAsync(accessToken, connection.TransactionsCursor);
            syncLog.TransactionsRetrieved = sync.Added.Count + sync.Modified.Count;

            var touchedIds = sync.Added.Concat(sync.Modified)
                .Select(t => FingerprintPrefix + t.TransactionId)
                .Concat(sync.RemovedTransactionIds.Select(id => FingerprintPrefix + id))
                .ToList();

            var existing = await _dbContext.FinancialTransactions
                .Where(t => t.UserId == connection.UserId &&
                            t.ImportFingerprint != null &&
                            touchedIds.Contains(t.ImportFingerprint))
                .ToDictionaryAsync(t => t.ImportFingerprint!);

            int imported = 0, duplicates = 0, errors = 0;

            foreach (var txn in sync.Added)
            {
                // Pending transactions are skipped: when they post, Plaid removes the
                // pending one and adds the posted one with a new id.
                if (txn.Pending) continue;
                if (!accountMap.TryGetValue(txn.AccountId, out var account)) continue;

                var fingerprint = FingerprintPrefix + txn.TransactionId;
                if (existing.ContainsKey(fingerprint))
                {
                    duplicates++;
                    continue;
                }

                try
                {
                    var financialTxn = await CreateFinancialTransactionAsync(
                        connection.UserId, account, txn, fingerprint, categories);
                    existing[fingerprint] = financialTxn;

                    _dbContext.BankTransactions.Add(new BankTransaction
                    {
                        BankConnectionId = connection.Id,
                        FinancialTransactionId = financialTxn.Id,
                        BankTransactionId = txn.TransactionId,
                        TransactionDateUtc = txn.Date,
                        Amount = txn.Amount,
                        Currency = txn.Currency,
                        Description = Truncate(txn.MerchantName ?? txn.Name, 500),
                        Merchant = txn.MerchantName is null ? null : Truncate(txn.MerchantName, 200),
                        AccountType = account.AccountType,
                        MaskedAccountNumber = account.Mask,
                        Status = 1, // Posted
                        TransactionHash = Hash(txn.TransactionId),
                        AutoMatchedCategory = txn.DetailedCategory ?? txn.PrimaryCategory,
                        ImportedAtUtc = DateTime.UtcNow
                    });
                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error importing Plaid transaction {TransactionId}", txn.TransactionId);
                    errors++;
                }
            }

            foreach (var txn in sync.Modified)
            {
                if (txn.Pending) continue;
                if (!existing.TryGetValue(FingerprintPrefix + txn.TransactionId, out var ft)) continue;

                // Keep the user's category choice; refresh the bank-owned fields.
                ft.Amount = Math.Round(Math.Abs(txn.Amount), 2);
                ft.Type = txn.Amount < 0 ? TransactionType.Income : TransactionType.Expense;
                ft.TransactionDateUtc = txn.Date;
                ft.Description = Truncate(txn.MerchantName ?? txn.Name, 250);
                ft.Merchant = txn.MerchantName is null ? null : Truncate(txn.MerchantName, 200);
            }

            foreach (var removedId in sync.RemovedTransactionIds)
            {
                if (existing.TryGetValue(FingerprintPrefix + removedId, out var ft))
                {
                    _dbContext.FinancialTransactions.Remove(ft);
                }
            }

            // Plaid may return an empty page with NOT_READY right after linking;
            // don't advance the cursor until it actually delivers history.
            if (sync.HistoryReady || sync.Added.Count > 0)
            {
                connection.TransactionsCursor = sync.NextCursor;
            }

            await _dbContext.SaveChangesAsync();

            // 3. Make app balances match the bank
            await AlignBalancesAsync(plaidAccounts.Accounts, accountMap);

            // 4. Recurring income / bills
            await SyncRecurringAsync(connection, accessToken, accountMap, categories);

            connection.LastSyncAtUtc = DateTime.UtcNow;
            connection.UpdatedAtUtc = DateTime.UtcNow;
            connection.Status = 1; // Connected
            connection.ErrorMessage = null;

            syncLog.TransactionsImported = imported;
            syncLog.DuplicatesSkipped = duplicates;
            syncLog.ErrorCount = errors;

            _logger.LogInformation(
                "Sync {ConnectionId}: {Imported} imported, {Duplicates} duplicates, {Errors} errors, ready={Ready}",
                bankConnectionId, imported, duplicates, errors, sync.HistoryReady);

            // "Pending" tells the UI Plaid is still preparing history and to retry shortly.
            return await FinishAsync(syncLog, sync.HistoryReady || imported > 0 ? "Success" : "Pending", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed for connection {ConnectionId}", bankConnectionId);
            _dbContext.ChangeTracker.Clear();

            var connection = await _dbContext.BankConnections.FindAsync(bankConnectionId);
            if (connection != null)
            {
                connection.ErrorMessage = Truncate(ex.Message, 500);
                // ITEM_LOGIN_REQUIRED means the user must re-authenticate with their bank
                if (ex.Message.Contains("ITEM_LOGIN_REQUIRED")) connection.Status = 2; // Error
            }

            syncLog.DetailedLog = ex.StackTrace;
            return await FinishAsync(syncLog, "Failed", ex.Message);
        }
    }

    public async Task<IEnumerable<BankSyncLog>> SyncAllConnectionsAsync()
    {
        var candidates = await _dbContext.BankConnections
            .Where(bc => !bc.IsDeleted && bc.Status == 1)
            .Select(bc => new { bc.Id, bc.UserId })
            .ToListAsync();

        // Connections stay linked after a downgrade, but only users whose
        // plan includes bank sync are refreshed (and cost Plaid calls).
        var entitledUsers = new HashSet<Guid>();
        foreach (var userId in candidates.Select(x => x.UserId).Distinct())
        {
            if (await _entitlements.GetPlanAsync(userId) >= SubscriptionPlan.Bank)
                entitledUsers.Add(userId);
        }

        var connectionIds = candidates
            .Where(x => entitledUsers.Contains(x.UserId))
            .Select(x => x.Id)
            .ToList();

        _logger.LogInformation(
            "Starting batch sync of {Count} connections ({Skipped} skipped: plan does not include bank sync)",
            connectionIds.Count,
            candidates.Count - connectionIds.Count);

        var logs = new List<BankSyncLog>();
        foreach (var id in connectionIds)
        {
            _dbContext.ChangeTracker.Clear();
            logs.Add(await SyncTransactionsAsync(id));
        }
        return logs;
    }

    public async Task<IEnumerable<BankSyncLog>> GetSyncHistoryAsync(
        Guid bankConnectionId,
        int pageNumber = 1,
        int pageSize = 10)
    {
        return await _dbContext.BankSyncLogs
            .Where(sl => sl.BankConnectionId == bankConnectionId)
            .OrderByDescending(sl => sl.StartedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // ------------------------------------------------------------------
    // Accounts
    // ------------------------------------------------------------------

    private async Task<Dictionary<string, FinancialAccount>> UpsertAccountsAsync(
        BankConnection connection,
        List<PlaidAccount> plaidAccounts)
    {
        var existing = await _dbContext.FinancialAccounts
            .Where(a => a.BankConnectionId == connection.Id && a.ExternalAccountId != null)
            .ToDictionaryAsync(a => a.ExternalAccountId!);

        foreach (var pa in plaidAccounts)
        {
            var accountType = MapAccountType(pa);
            if (accountType == null) continue; // investments etc. are not tracked
            if (existing.ContainsKey(pa.AccountId)) continue;

            var name = $"{connection.InstitutionName} {pa.Name}";
            if (!string.IsNullOrEmpty(pa.Mask)) name += $" ••{pa.Mask}";

            var account = new FinancialAccount
            {
                UserId = connection.UserId,
                Name = Truncate(name, 150),
                AccountType = accountType,
                Currency = pa.Currency,
                OpeningBalance = 0, // set by AlignBalancesAsync
                BankConnectionId = connection.Id,
                ExternalAccountId = pa.AccountId,
                Mask = pa.Mask
            };
            _dbContext.FinancialAccounts.Add(account);
            existing[pa.AccountId] = account;
        }

        await _dbContext.SaveChangesAsync();
        return existing;
    }

    private static string? MapAccountType(PlaidAccount pa) => pa.Type switch
    {
        "credit" => "CreditCard",
        "depository" when pa.Subtype is "savings" or "money market" or "cd" => "Savings",
        "depository" => "Chequing",
        _ => null
    };

    /// <summary>
    /// The app derives balance = OpeningBalance + income - expenses (+/- transfers).
    /// Back-solve OpeningBalance so the derived balance equals the bank's current balance.
    /// Credit card balances from Plaid are amounts owed, so they are stored as negative.
    /// </summary>
    private async Task AlignBalancesAsync(
        List<PlaidAccount> plaidAccounts,
        Dictionary<string, FinancialAccount> accountMap)
    {
        var now = DateTime.UtcNow;

        foreach (var pa in plaidAccounts)
        {
            if (!accountMap.TryGetValue(pa.AccountId, out var account)) continue;

            var bankBalance = account.AccountType == "CreditCard" ? -pa.Balance : pa.Balance;

            var flows = await _dbContext.FinancialTransactions
                .Where(t => t.TransactionDateUtc <= now &&
                            (t.FinancialAccountId == account.Id || t.DestinationAccountId == account.Id))
                .Select(t => new { t.Type, t.Amount, t.FinancialAccountId, t.DestinationAccountId })
                .ToListAsync();

            var net = flows.Sum(t =>
                t.Type == TransactionType.Income && t.FinancialAccountId == account.Id ? t.Amount :
                t.Type == TransactionType.Expense && t.FinancialAccountId == account.Id ? -t.Amount :
                t.Type == TransactionType.Transfer && t.DestinationAccountId == account.Id ? t.Amount :
                t.Type == TransactionType.Transfer && t.FinancialAccountId == account.Id ? -t.Amount :
                0m);

            account.OpeningBalance = bankBalance - net;
        }

        await _dbContext.SaveChangesAsync();
    }

    // ------------------------------------------------------------------
    // Transactions
    // ------------------------------------------------------------------

    private async Task<FinancialTransaction> CreateFinancialTransactionAsync(
        Guid userId,
        FinancialAccount account,
        PlaidTransaction txn,
        string fingerprint,
        CategoryResolver categories)
    {
        // Plaid: positive amount = money leaving the account, negative = money coming in
        var type = txn.Amount < 0 ? TransactionType.Income : TransactionType.Expense;
        var amount = Math.Round(Math.Abs(txn.Amount), 2);
        var description = Truncate(txn.MerchantName ?? txn.Name, 250);

        // User-defined rules win over Plaid's category
        Guid? categoryId = null;
        var rule = await _categoryRuleService.EvaluateTransactionAsync(
            userId, txn.Name, txn.MerchantName, amount, (int)type);
        if (rule.CategoryId.HasValue && await categories.MatchesTypeAsync(rule.CategoryId.Value, type))
        {
            categoryId = rule.CategoryId;
        }

        categoryId ??= await categories.ResolveAsync(
            PlaidCategoryMapper.Map(txn.PrimaryCategory, txn.DetailedCategory, type), type);

        var ft = new FinancialTransaction
        {
            UserId = userId,
            FinancialAccountId = account.Id,
            CategoryId = categoryId,
            Type = type,
            Amount = amount,
            Description = description,
            Merchant = txn.MerchantName is null ? null : Truncate(txn.MerchantName, 200),
            TransactionDateUtc = txn.Date,
            ImportFingerprint = fingerprint
        };
        _dbContext.FinancialTransactions.Add(ft);
        return ft;
    }

    // ------------------------------------------------------------------
    // Recurring
    // ------------------------------------------------------------------

    private async Task SyncRecurringAsync(
        BankConnection connection,
        string accessToken,
        Dictionary<string, FinancialAccount> accountMap,
        CategoryResolver categories)
    {
        PlaidRecurringResult recurring;
        try
        {
            recurring = await _plaidService.GetRecurringStreamsAsync(accessToken);
        }
        catch (Exception ex)
        {
            // Commonly PRODUCT_NOT_READY right after linking; next sync will pick it up.
            _logger.LogWarning("Recurring detection skipped for {ConnectionId}: {Message}", connection.Id, ex.Message);
            return;
        }

        var streams = recurring.Inflows.Select(s => (Stream: s, Type: TransactionType.Income))
            .Concat(recurring.Outflows.Select(s => (Stream: s, Type: TransactionType.Expense)))
            .ToList();

        var streamIds = streams.Select(s => s.Stream.StreamId).ToList();
        var existing = await _dbContext.RecurringTransactions
            .Where(r => r.UserId == connection.UserId &&
                        r.ExternalStreamId != null &&
                        streamIds.Contains(r.ExternalStreamId))
            .ToDictionaryAsync(r => r.ExternalStreamId!);

        foreach (var (stream, type) in streams)
        {
            if (!accountMap.TryGetValue(stream.AccountId, out var account)) continue;
            var frequency = MapFrequency(stream.Frequency);
            if (frequency == null) continue;

            existing.TryGetValue(stream.StreamId, out var schedule);

            // Only surface streams Plaid is confident about; keep updating ones we already track.
            if (schedule == null && !(stream.IsActive && stream.Status == "MATURE")) continue;

            if (schedule == null)
            {
                schedule = new RecurringTransaction
                {
                    UserId = connection.UserId,
                    ExternalStreamId = stream.StreamId,
                    CategoryId = await categories.ResolveAsync(RecurringCategory(stream, type), type)
                };
                _dbContext.RecurringTransactions.Add(schedule);
            }

            schedule.FinancialAccountId = account.Id;
            schedule.Type = type;
            schedule.Amount = Math.Round(Math.Abs(stream.LastAmount != 0 ? stream.LastAmount : stream.AverageAmount), 2);
            var streamName = !string.IsNullOrWhiteSpace(stream.MerchantName) ? stream.MerchantName
                : !string.IsNullOrWhiteSpace(stream.Description) ? stream.Description
                : type == TransactionType.Income ? "Recurring income" : "Recurring payment";
            schedule.Description = Truncate(streamName, 250);
            schedule.Frequency = frequency.Value;
            schedule.StartDateUtc = stream.FirstDate;
            schedule.NextOccurrenceUtc = stream.PredictedNextDate ?? NextAfter(stream.LastDate, frequency.Value);
            schedule.Status = stream.IsActive && stream.Status != "TOMBSTONED"
                ? RecurringStatus.Active
                : RecurringStatus.Cancelled;

            // Link the actual imported occurrences to the schedule
            var fingerprints = stream.TransactionIds.Select(id => FingerprintPrefix + id).ToList();
            if (fingerprints.Count > 0)
            {
                var occurrences = await _dbContext.FinancialTransactions
                    .Where(t => t.UserId == connection.UserId &&
                                t.ImportFingerprint != null &&
                                fingerprints.Contains(t.ImportFingerprint))
                    .ToListAsync();
                foreach (var occurrence in occurrences)
                {
                    occurrence.RecurringTransactionId = schedule.Id;
                }
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    /// <summary>Recurring entertainment/service charges (Netflix, Spotify…) are subscriptions.</summary>
    private static string RecurringCategory(PlaidRecurringStream stream, TransactionType type)
    {
        var mapped = PlaidCategoryMapper.Map(stream.PrimaryCategory, null, type);
        return type == TransactionType.Expense &&
               (mapped is "Entertainment" or "Other Expenses" || stream.PrimaryCategory == "GENERAL_SERVICES")
            ? "Subscriptions"
            : mapped;
    }

    private static RecurrenceFrequency? MapFrequency(string plaidFrequency) => plaidFrequency switch
    {
        "WEEKLY" => RecurrenceFrequency.Weekly,
        "BIWEEKLY" => RecurrenceFrequency.Biweekly,
        // App has no semi-monthly; biweekly is the closest cadence
        "SEMI_MONTHLY" => RecurrenceFrequency.Biweekly,
        "MONTHLY" => RecurrenceFrequency.Monthly,
        "ANNUALLY" => RecurrenceFrequency.Yearly,
        _ => null
    };

    private static DateTime NextAfter(DateTime last, RecurrenceFrequency f) => f switch
    {
        RecurrenceFrequency.Daily => last.AddDays(1),
        RecurrenceFrequency.Weekly => last.AddDays(7),
        RecurrenceFrequency.Biweekly => last.AddDays(14),
        RecurrenceFrequency.Monthly => last.AddMonths(1),
        RecurrenceFrequency.Yearly => last.AddYears(1),
        _ => last.AddMonths(1)
    };

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<BankSyncLog> FinishAsync(BankSyncLog log, string status, string? error)
    {
        log.Status = status;
        log.ErrorMessage = error;
        log.CompletedAtUtc = DateTime.UtcNow;
        _dbContext.BankSyncLogs.Add(log);
        await _dbContext.SaveChangesAsync();
        return log;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string Hash(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));

    /// <summary>
    /// Finds the user's category by name and type, creating it if missing. Cached per sync.
    /// </summary>
    private sealed class CategoryResolver
    {
        private readonly FinanceDbContext _db;
        private readonly Guid _userId;
        private Dictionary<(TransactionType, string), TransactionCategory>? _byName;

        public CategoryResolver(FinanceDbContext db, Guid userId)
        {
            _db = db;
            _userId = userId;
        }

        private async Task<Dictionary<(TransactionType, string), TransactionCategory>> LoadAsync()
        {
            if (_byName != null) return _byName;
            var all = await _db.TransactionCategories.Where(c => c.UserId == _userId).ToListAsync();
            _byName = all
                .GroupBy(c => (c.Type, c.Name.ToLowerInvariant()))
                .ToDictionary(g => g.Key, g => g.First());
            return _byName;
        }

        public async Task<Guid> ResolveAsync(string name, TransactionType type)
        {
            var map = await LoadAsync();
            if (map.TryGetValue((type, name.ToLowerInvariant()), out var category)) return category.Id;

            category = new TransactionCategory { UserId = _userId, Name = name, Type = type };
            _db.TransactionCategories.Add(category);
            map[(type, name.ToLowerInvariant())] = category;
            return category.Id;
        }

        public async Task<bool> MatchesTypeAsync(Guid categoryId, TransactionType type)
        {
            var map = await LoadAsync();
            return map.Values.Any(c => c.Id == categoryId && c.Type == type);
        }
    }
}

/// <summary>
/// Maps Plaid's personal_finance_category to the app's default category names
/// (see DefaultCategorySeeder). Unknown categories fall back to Other Income/Expenses.
/// </summary>
public static class PlaidCategoryMapper
{
    public static string Map(string? primary, string? detailed, TransactionType type)
    {
        if (type == TransactionType.Income)
        {
            return (primary, detailed) switch
            {
                (_, "INCOME_WAGES") => "Salary",
                (_, "INCOME_DIVIDENDS" or "INCOME_INTEREST_EARNED" or "INCOME_RETIREMENT_PENSION") => "Investment Income",
                ("TRANSFER_IN" or "TRANSFER_OUT" or "LOAN_PAYMENTS", _) => "Transfers In",
                _ => "Other Income"
            };
        }

        return (primary, detailed) switch
        {
            (_, "FOOD_AND_DRINK_GROCERIES") => "Groceries",
            ("FOOD_AND_DRINK", _) => "Restaurants",
            (_, "RENT_AND_UTILITIES_RENT") => "Rent & Housing",
            ("RENT_AND_UTILITIES", _) => "Utilities",
            ("HOME_IMPROVEMENT", _) => "Rent & Housing",
            ("TRANSPORTATION", _) => "Transportation",
            ("TRAVEL", _) => "Travel",
            ("ENTERTAINMENT", _) => "Entertainment",
            ("MEDICAL" or "PERSONAL_CARE", _) => "Health",
            ("GENERAL_MERCHANDISE", _) => "Shopping",
            (_, "GENERAL_SERVICES_EDUCATION") => "Education",
            (_, "GENERAL_SERVICES_INSURANCE") => "Insurance",
            ("BANK_FEES", _) => "Bank Fees",
            ("TRANSFER_IN" or "TRANSFER_OUT" or "LOAN_PAYMENTS", _) => "Transfers & Payments",
            _ => "Other Expenses"
        };
    }
}
