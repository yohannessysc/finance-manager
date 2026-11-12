namespace FinanceManager.Infrastructure.Services.Plaid;

/// <summary>
/// Plaid configuration from appsettings
/// </summary>
public class PlaidOptions
{
    public const string SectionName = "Plaid";

    /// <summary>Plaid Client ID</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Plaid Secret</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>Plaid Environment: "sandbox", "development", or "production"</summary>
    public string Environment { get; set; } = "development";

    /// <summary>Redirect URI for web Plaid Link flow</summary>
    public string? RedirectUri { get; set; }

    /// <summary>Android package name for Plaid Link iOS</summary>
    public string? AndroidPackageName { get; set; }
}
