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

    /// <summary>
    /// Public HTTPS URL Plaid posts webhooks to, e.g.
    /// https://app.example.com/api/v1/bank/plaid/webhook. Optional: without it
    /// the scheduled sync still picks up changes.
    /// </summary>
    public string? WebhookUrl { get; set; }
}
