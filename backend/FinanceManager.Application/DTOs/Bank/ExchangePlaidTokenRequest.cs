namespace FinanceManager.Application.DTOs.Bank;

/// <summary>
/// Response after Plaid Link is exchanged for access token
/// </summary>
public class ExchangePlaidTokenRequest
{
    /// <summary>Public token from Plaid Link (valid for 30 minutes)</summary>
    public string PublicToken { get; set; } = string.Empty;

    /// <summary>Metadata about the linked account from Plaid Link</summary>
    public PlaidMetadata? Metadata { get; set; }

    /// <summary>Financial account to link to (optional)</summary>
    public Guid? FinancialAccountId { get; set; }
}

/// <summary>
/// Plaid Link metadata returned on successful connection
/// </summary>
public class PlaidMetadata
{
    /// <summary>Account ID from user's selection in Plaid Link</summary>
    public string? AccountId { get; set; }

    /// <summary>Institution ID (Plaid institution code)</summary>
    public string? InstitutionId { get; set; }

    /// <summary>Institution name (e.g., "Royal Bank of Canada")</summary>
    public string? InstitutionName { get; set; }

    /// <summary>Account subtype (e.g., "checking")</summary>
    public string? AccountSubtype { get; set; }

    /// <summary>Account type (e.g., "depository")</summary>
    public string? AccountType { get; set; }

    /// <summary>Mask (last 4 digits of account)</summary>
    public string? Mask { get; set; }
}
