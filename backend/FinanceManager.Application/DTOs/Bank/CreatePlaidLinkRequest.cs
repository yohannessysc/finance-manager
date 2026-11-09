namespace FinanceManager.Application.DTOs.Bank;

/// <summary>
/// Request to initiate Plaid Link setup
/// </summary>
public class CreatePlaidLinkRequest
{
    /// <summary>Financial account to link to (optional, can link multiple accounts to one bank connection)</summary>
    public Guid? FinancialAccountId { get; set; }

    /// <summary>User email for Plaid Link</summary>
    public string? UserEmail { get; set; }

    /// <summary>User full name for Plaid Link</summary>
    public string? UserFullName { get; set; }
}
