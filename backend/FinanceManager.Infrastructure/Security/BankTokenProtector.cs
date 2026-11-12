using FinanceManager.Application.Interfaces;

using Microsoft.AspNetCore.DataProtection;

namespace FinanceManager.Infrastructure.Security;

public sealed class BankTokenProtector : IBankTokenProtector
{
    // Changing the purpose string makes existing tokens unreadable.
    private const string Purpose = "FinanceManager.BankConnections.AccessToken.v1";

    // Plaid access tokens look like "access-<env>-<uuid>"; protected
    // payloads are base64url and never contain this prefix.
    private const string PlaidTokenPrefix = "access-";

    private readonly IDataProtector _protector;

    public BankTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string accessToken) =>
        _protector.Protect(accessToken);

    public string Unprotect(string storedToken) =>
        IsLegacyPlaintext(storedToken)
            ? storedToken
            : _protector.Unprotect(storedToken);

    public bool IsLegacyPlaintext(string storedToken) =>
        storedToken.StartsWith(PlaidTokenPrefix, StringComparison.Ordinal);
}
