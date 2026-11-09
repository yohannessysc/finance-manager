namespace FinanceManager.Application.DTOs.Bank;

/// <summary>
/// Response for creating Plaid Link
/// </summary>
public class PlaidLinkTokenResponse
{
    /// <summary>Link token for Plaid Link initialization (valid for 10 minutes)</summary>
    public string LinkToken { get; set; } = string.Empty;

    /// <summary>Expiration time of the link token</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Request ID for debugging with Plaid support</summary>
    public string? RequestId { get; set; }
}
