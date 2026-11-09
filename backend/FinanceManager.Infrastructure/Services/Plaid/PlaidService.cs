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
        string[]? institutionIds = null);

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
        string[]? institutionIds = null)
    {
        try
        {
            _logger.LogInformation("Creating Plaid Link token for user {UserId}", userId);

            var request = new
            {
                client_id = _options.ClientId,
                secret = _options.Secret,
                user = new
                {
                    client_user_id = userId,
                    email_address = userEmail,
                    phone_number = userName
                },
                client_name = "Finance Manager",
                language = "en",
                country_codes = countryCodes ?? new[] { "CA" },
                products = new[] { "auth", "transactions" },
                redirect_uri = _options.RedirectUri
            };

            var response = await _httpClient.PostAsJsonAsync("/link/token/create", request);
            response.EnsureSuccessStatusCode();

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
            response.EnsureSuccessStatusCode();

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
        try
        {
            _logger.LogInformation("Fetching accounts for Plaid item");

            var request = new
            {
                client_id = _options.ClientId,
                secret = _options.Secret,
                access_token = accessToken
            };

            var response = await _httpClient.PostAsJsonAsync("/auth/get", request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accounts = new List<PlaidAccount>();
            if (root.TryGetProperty("accounts", out var accountsArray))
            {
                foreach (var account in accountsArray.EnumerateArray())
                {
                    var balance = 0m;
                    if (account.TryGetProperty("balances", out var balances))
                    {
                        if (balances.TryGetProperty("current", out var currentBalance))
                        {
                            balance = (decimal?)currentBalance.GetDecimal() ?? 0m;
                        }
                    }

                    accounts.Add(new PlaidAccount
                    {
                        AccountId = account.GetProperty("account_id").GetString() ?? "",
                        Name = account.GetProperty("name").GetString() ?? "Unknown",
                        Subtype = account.GetProperty("subtype").GetString() ?? "Unknown",
                        Balance = balance,
                        Currency = account.TryGetProperty("balances", out var bal) && 
                                  bal.TryGetProperty("iso_currency_code", out var curr)
                            ? curr.GetString() ?? "CAD" : "CAD"
                    });
                }
            }

            var itemId = root.GetProperty("item").GetProperty("item_id").GetString() ?? "";

            return new PlaidAccountsResult
            {
                ItemId = itemId,
                Accounts = accounts,
                RequestId = root.GetProperty("request_id").GetString() ?? ""
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch accounts");
            throw;
        }
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
            response.EnsureSuccessStatusCode();

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
            response.EnsureSuccessStatusCode();

            _logger.LogInformation("Successfully removed Plaid item");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove Plaid item");
            throw;
        }
    }
}
