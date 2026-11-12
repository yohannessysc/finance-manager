using System.Text.Json;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Services.Plaid;

/// <summary>
/// Wrapper around Plaid API for bank integration
/// </summary>
public interface IPlaidService
{
    /// <summary>
    /// Create a Plaid Link token for frontend to initialize Plaid Link
    /// </summary>
    Task<PlaidLinkTokenResult> CreateLinkTokenAsync(
        string userId,
        string? userEmail = null,
        string? userName = null,
        string[]? countryCodes = null,
        string[]? institutionIds = null,
        string? accessToken = null);

    /// <summary>
    /// Exchange public token for access token after user completes Plaid Link
    /// </summary>
    Task<PlaidTokenExchangeResult> ExchangePublicTokenAsync(string publicToken);

    /// <summary>
    /// Get accounts and balances from a bank item
    /// </summary>
    Task<PlaidAccountsResult> GetAccountsAsync(string accessToken);

    /// <summary>
    /// Get transactions from a bank item
    /// </summary>
    Task<PlaidTransactionsResult> GetTransactionsAsync(
        string accessToken,
        DateTime startDate,
        DateTime endDate,
        int offset = 0,
        int limit = 100);

    /// <summary>
    /// Remove an item (revoke access token)
    /// </summary>
    Task RemoveItemAsync(string accessToken);

    /// <summary>
    /// Incremental transaction sync (/transactions/sync). Pass the stored cursor
    /// (null for the first sync). Follows has_more pages internally.
    /// </summary>
    Task<PlaidSyncResult> SyncTransactionsAsync(string accessToken, string? cursor);

    /// <summary>
    /// Recurring inflow/outflow streams detected by Plaid (/transactions/recurring/get)
    /// </summary>
    Task<PlaidRecurringResult> GetRecurringStreamsAsync(string accessToken);
}

/// <summary>
/// Result types returned by PlaidService
/// </summary>

public class PlaidLinkTokenResult
{
    public string LinkToken { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public class PlaidTokenExchangeResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

public class PlaidAccountsResult
{
    public string ItemId { get; set; } = string.Empty;
    public List<PlaidAccount> Accounts { get; set; } = new();
    public string RequestId { get; set; } = string.Empty;
}

public class PlaidAccount
{
    public string AccountId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Subtype { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "CAD";
    /// <summary>depository, credit, loan, investment, other</summary>
    public string Type { get; set; } = string.Empty;
    public string? Mask { get; set; }
}

public class PlaidTransactionsResult
{
    public string ItemId { get; set; } = string.Empty;
    public List<PlaidTransaction> Transactions { get; set; } = new();
    public int TotalTransactions { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

public class PlaidTransaction
{
    public string TransactionId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CAD";
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public List<string> Categories { get; set; } = new();
    public bool Pending { get; set; }
    /// <summary>Plaid personal_finance_category.primary, e.g. FOOD_AND_DRINK</summary>
    public string? PrimaryCategory { get; set; }
    /// <summary>Plaid personal_finance_category.detailed, e.g. FOOD_AND_DRINK_GROCERIES</summary>
    public string? DetailedCategory { get; set; }
}

public class PlaidSyncResult
{
    public List<PlaidTransaction> Added { get; set; } = new();
    public List<PlaidTransaction> Modified { get; set; } = new();
    public List<string> RemovedTransactionIds { get; set; } = new();
    public string? NextCursor { get; set; }
    /// <summary>
    /// False right after linking, while Plaid is still pulling history
    /// (transactions_update_status = NOT_READY).
    /// </summary>
    public bool HistoryReady { get; set; } = true;
}

public class PlaidRecurringResult
{
    public List<PlaidRecurringStream> Inflows { get; set; } = new();
    public List<PlaidRecurringStream> Outflows { get; set; } = new();
}

public class PlaidRecurringStream
{
    public string StreamId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    /// <summary>WEEKLY, BIWEEKLY, SEMI_MONTHLY, MONTHLY, ANNUALLY, UNKNOWN</summary>
    public string Frequency { get; set; } = string.Empty;
    public decimal AverageAmount { get; set; }
    public decimal LastAmount { get; set; }
    public DateTime FirstDate { get; set; }
    public DateTime LastDate { get; set; }
    public DateTime? PredictedNextDate { get; set; }
    public bool IsActive { get; set; }
    /// <summary>EARLY_DETECTION, MATURE, TOMBSTONED, UNKNOWN</summary>
    public string Status { get; set; } = string.Empty;
    public string? PrimaryCategory { get; set; }
    public List<string> TransactionIds { get; set; } = new();
}

/// <summary>
/// Implementation of IPlaidService using Plaid REST API
/// </summary>
public class PlaidService : IPlaidService
{
    private readonly PlaidOptions _options;
    private readonly ILogger<PlaidService> _logger;
    private readonly HttpClient _httpClient;

    private static readonly Dictionary<string, string> EnvironmentBaseUrls = new()
    {
        { "sandbox", "https://sandbox.plaid.com" },
        { "development", "https://development.plaid.com" },
        { "production", "https://production.plaid.com" }
    };

    public PlaidService(
        IOptions<PlaidOptions> options,
        ILogger<PlaidService> logger,
        HttpClient httpClient)
    {
        _options = options.Value;
        _logger = logger;
        _httpClient = httpClient;

        var baseUrl = EnvironmentBaseUrls.GetValueOrDefault(_options.Environment, EnvironmentBaseUrls["development"]);
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    public async Task<PlaidLinkTokenResult> CreateLinkTokenAsync(
        string userId,
        string? userEmail = null,
        string? userName = null,
        string[]? countryCodes = null,
        string[]? institutionIds = null,
        string? accessToken = null)
    {
        try
        {
            _logger.LogInformation("Creating Plaid Link token for user {UserId}", userId);

            var userDict = new Dictionary<string, object?>
            {
                { "client_user_id", userId }
            };
            if (!string.IsNullOrEmpty(userEmail))
            {
                userDict["email_address"] = userEmail;
            }

            var request = new Dictionary<string, object?>
            {
                ["client_id"] = _options.ClientId,
                ["secret"] = _options.Secret,
                ["user"] = userDict,
                ["client_name"] = "Finance Manager",
                ["language"] = "en",
                ["country_codes"] = countryCodes ?? new[] { "CA" }
            };

            // Update mode (re-authenticating an existing item) passes the
            // item's access token instead of products.
            if (accessToken is null)
                request["products"] = new[] { "auth", "transactions" };
            else
                request["access_token"] = accessToken;

            var response = await _httpClient.PostAsJsonAsync("/link/token/create", request);
            await EnsurePlaidSuccessAsync(response);

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new PlaidLinkTokenResult
            {
                LinkToken = root.GetProperty("link_token").GetString() ?? "",
                RequestId = root.GetProperty("request_id").GetString() ?? "",
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Plaid Link token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<PlaidTokenExchangeResult> ExchangePublicTokenAsync(string publicToken)
    {
        try
        {
            _logger.LogInformation("Exchanging Plaid public token");

            var request = new
            {
                client_id = _options.ClientId,
                secret = _options.Secret,
                public_token = publicToken
            };

            var response = await _httpClient.PostAsJsonAsync("/item/public_token/exchange", request);
            await EnsurePlaidSuccessAsync(response);

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new PlaidTokenExchangeResult
            {
                AccessToken = root.GetProperty("access_token").GetString() ?? "",
                ItemId = root.GetProperty("item_id").GetString() ?? "",
                RequestId = root.GetProperty("request_id").GetString() ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to exchange Plaid public token");
            throw;
        }
    }

    public async Task<PlaidAccountsResult> GetAccountsAsync(string accessToken)
    {
        _logger.LogInformation("Fetching accounts for Plaid item");

        using var doc = await PostAsync("/accounts/get", new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            access_token = accessToken
        });
        var root = doc.RootElement;

        var accounts = new List<PlaidAccount>();
        foreach (var account in root.GetProperty("accounts").EnumerateArray())
        {
            var balances = account.GetProperty("balances");
            accounts.Add(new PlaidAccount
            {
                AccountId = Str(account, "account_id") ?? "",
                Name = Str(account, "name") ?? "Account",
                Subtype = Str(account, "subtype") ?? "",
                Type = Str(account, "type") ?? "",
                Mask = Str(account, "mask"),
                Balance = Dec(balances, "current") ?? Dec(balances, "available") ?? 0m,
                Currency = Str(balances, "iso_currency_code") ?? "CAD"
            });
        }

        return new PlaidAccountsResult
        {
            ItemId = Str(root.GetProperty("item"), "item_id") ?? "",
            Accounts = accounts,
            RequestId = Str(root, "request_id") ?? ""
        };
    }

    public async Task<PlaidSyncResult> SyncTransactionsAsync(string accessToken, string? cursor)
    {
        var result = new PlaidSyncResult();
        var currentCursor = cursor;
        var hasMore = true;
        var pages = 0;

        while (hasMore && pages++ < 50)
        {
            var body = new Dictionary<string, object?>
            {
                ["client_id"] = _options.ClientId,
                ["secret"] = _options.Secret,
                ["access_token"] = accessToken,
                ["count"] = 500
            };
            if (!string.IsNullOrEmpty(currentCursor)) body["cursor"] = currentCursor;

            using var doc = await PostAsync("/transactions/sync", body);
            var root = doc.RootElement;

            foreach (var t in root.GetProperty("added").EnumerateArray())
                result.Added.Add(ParseTransaction(t));
            foreach (var t in root.GetProperty("modified").EnumerateArray())
                result.Modified.Add(ParseTransaction(t));
            foreach (var t in root.GetProperty("removed").EnumerateArray())
            {
                var id = Str(t, "transaction_id");
                if (id != null) result.RemovedTransactionIds.Add(id);
            }

            if (Str(root, "transactions_update_status") == "NOT_READY")
                result.HistoryReady = false;

            currentCursor = Str(root, "next_cursor");
            hasMore = root.TryGetProperty("has_more", out var hm) && hm.GetBoolean();
        }

        result.NextCursor = currentCursor;
        _logger.LogInformation(
            "Plaid sync: {Added} added, {Modified} modified, {Removed} removed, ready={Ready}",
            result.Added.Count, result.Modified.Count, result.RemovedTransactionIds.Count, result.HistoryReady);
        return result;
    }

    public async Task<PlaidRecurringResult> GetRecurringStreamsAsync(string accessToken)
    {
        using var doc = await PostAsync("/transactions/recurring/get", new
        {
            client_id = _options.ClientId,
            secret = _options.Secret,
            access_token = accessToken
        });
        var root = doc.RootElement;

        return new PlaidRecurringResult
        {
            Inflows = root.GetProperty("inflow_streams").EnumerateArray().Select(ParseStream).ToList(),
            Outflows = root.GetProperty("outflow_streams").EnumerateArray().Select(ParseStream).ToList()
        };
    }

    private async Task<JsonDocument> PostAsync(string path, object body)
    {
        var response = await _httpClient.PostAsJsonAsync(path, body);
        await EnsurePlaidSuccessAsync(response);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static PlaidTransaction ParseTransaction(JsonElement t)
    {
        string? primary = null, detailed = null;
        if (t.TryGetProperty("personal_finance_category", out var pfc) &&
            pfc.ValueKind == JsonValueKind.Object)
        {
            primary = Str(pfc, "primary");
            detailed = Str(pfc, "detailed");
        }

        // Prefer the date the purchase was made over the posting date
        var date = Date(t, "authorized_date") ?? Date(t, "date") ?? DateTime.UtcNow.Date;

        return new PlaidTransaction
        {
            TransactionId = Str(t, "transaction_id") ?? "",
            AccountId = Str(t, "account_id") ?? "",
            Amount = Dec(t, "amount") ?? 0m,
            Currency = Str(t, "iso_currency_code") ?? "CAD",
            Date = date,
            Name = Str(t, "name") ?? "Unknown",
            MerchantName = Str(t, "merchant_name"),
            Pending = t.TryGetProperty("pending", out var p) && p.ValueKind == JsonValueKind.True,
            PrimaryCategory = primary,
            DetailedCategory = detailed,
            Categories = primary != null ? new List<string> { primary } : new List<string>()
        };
    }

    private static PlaidRecurringStream ParseStream(JsonElement s)
    {
        string? primary = null;
        if (s.TryGetProperty("personal_finance_category", out var pfc) &&
            pfc.ValueKind == JsonValueKind.Object)
            primary = Str(pfc, "primary");

        return new PlaidRecurringStream
        {
            StreamId = Str(s, "stream_id") ?? "",
            AccountId = Str(s, "account_id") ?? "",
            Description = Str(s, "description") ?? "",
            MerchantName = Str(s, "merchant_name"),
            Frequency = Str(s, "frequency") ?? "UNKNOWN",
            AverageAmount = s.TryGetProperty("average_amount", out var avg) ? Dec(avg, "amount") ?? 0m : 0m,
            LastAmount = s.TryGetProperty("last_amount", out var last) ? Dec(last, "amount") ?? 0m : 0m,
            FirstDate = Date(s, "first_date") ?? DateTime.UtcNow.Date,
            LastDate = Date(s, "last_date") ?? DateTime.UtcNow.Date,
            PredictedNextDate = Date(s, "predicted_next_date"),
            IsActive = s.TryGetProperty("is_active", out var a) && a.ValueKind == JsonValueKind.True,
            Status = Str(s, "status") ?? "UNKNOWN",
            PrimaryCategory = primary,
            TransactionIds = s.TryGetProperty("transaction_ids", out var ids)
                ? ids.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x != "").ToList()
                : new List<string>()
        };
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? Dec(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    private static DateTime? Date(JsonElement e, string name)
    {
        var str = Str(e, name);
        return str != null && DateTime.TryParse(str, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var d)
            ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc)
            : null;
    }

    public async Task<PlaidTransactionsResult> GetTransactionsAsync(
        string accessToken,
        DateTime startDate,
        DateTime endDate,
        int offset = 0,
        int limit = 100)
    {
        try
        {
            _logger.LogInformation(
                "Fetching transactions from {StartDate} to {EndDate}",
                startDate.Date,
                endDate.Date);

            var request = new
            {
                client_id = _options.ClientId,
                secret = _options.Secret,
                access_token = accessToken,
                start_date = startDate.Date.ToString("yyyy-MM-dd"),
                end_date = endDate.Date.ToString("yyyy-MM-dd"),
                options = new
                {
                    offset,
                    count = limit
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/transactions/get", request);
            await EnsurePlaidSuccessAsync(response);

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var transactions = new List<PlaidTransaction>();
            if (root.TryGetProperty("transactions", out var txnsArray))
            {
                foreach (var txn in txnsArray.EnumerateArray())
                {
                    var categories = new List<string>();
                    if (txn.TryGetProperty("personal_finance_category", out var pfc) &&
                        pfc.TryGetProperty("primary", out var primary))
                    {
                        categories.Add(primary.GetString() ?? "");
                    }

                    transactions.Add(new PlaidTransaction
                    {
                        TransactionId = txn.GetProperty("transaction_id").GetString() ?? "",
                        AccountId = txn.GetProperty("account_id").GetString() ?? "",
                        Amount = (decimal?)txn.GetProperty("amount").GetDecimal() ?? 0m,
                        Currency = txn.GetProperty("iso_currency_code").GetString() ?? "CAD",
                        Date = DateTime.Parse(txn.GetProperty("date").GetString() ?? DateTime.UtcNow.ToString("o")),
                        Name = txn.GetProperty("name").GetString() ?? "Unknown",
                        MerchantName = txn.TryGetProperty("merchant_name", out var merchant)
                            ? merchant.GetString() : null,
                        Categories = categories
                    });
                }
            }

            var itemId = root.GetProperty("item").GetProperty("item_id").GetString() ?? "";
            var totalTxns = root.GetProperty("total_transactions").GetInt32();

            return new PlaidTransactionsResult
            {
                ItemId = itemId,
                Transactions = transactions,
                TotalTransactions = totalTxns,
                RequestId = root.GetProperty("request_id").GetString() ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch transactions");
            throw;
        }
    }

    public async Task RemoveItemAsync(string accessToken)
    {
        try
        {
            _logger.LogInformation("Removing Plaid item");

            var request = new
            {
                client_id = _options.ClientId,
                secret = _options.Secret,
                access_token = accessToken
            };

            var response = await _httpClient.PostAsJsonAsync("/item/remove", request);
            await EnsurePlaidSuccessAsync(response);

            _logger.LogInformation("Successfully removed Plaid item");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove Plaid item");
            throw;
        }
    }

    /// <summary>
    /// Throws with Plaid's error_code / error_message instead of a generic HTTP status error
    /// </summary>
    private static async Task EnsurePlaidSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync();
        string message = body;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var code = root.TryGetProperty("error_code", out var c) ? c.GetString() : null;
            var msg = root.TryGetProperty("error_message", out var m) ? m.GetString() : null;
            message = $"Plaid {code}: {msg}";
        }
        catch (JsonException) { }

        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
