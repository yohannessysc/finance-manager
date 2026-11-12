namespace FinanceManager.Application.Interfaces;

// Encrypts bank provider access tokens before they are stored.
public interface IBankTokenProtector
{
    string Protect(string accessToken);

    string Unprotect(string storedToken);

    // True for tokens saved before encryption was introduced.
    bool IsLegacyPlaintext(string storedToken);
}
