using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services.Plaid;

public sealed record PlaidWebhookKey(string Kid, string Curve, string X, string Y, long? ExpiredAt);

// Verifies the Plaid-Verification header: an ES256 JWT signed by Plaid whose
// payload carries the SHA-256 of the exact request body.
// https://plaid.com/docs/api/webhooks/webhook-verification/
public sealed class PlaidWebhookVerifier
{
    private static readonly TimeSpan MaximumAge = TimeSpan.FromMinutes(5);

    // Keys are immutable per kid, so they are cached for the process
    // (static: the verifier itself is scoped, like its Plaid HttpClient).
    private static readonly ConcurrentDictionary<string, PlaidWebhookKey> Keys = new();
    private readonly IPlaidService _plaid;
    private readonly ILogger<PlaidWebhookVerifier> _logger;
    private readonly TimeProvider _time;

    public PlaidWebhookVerifier(
        IPlaidService plaid,
        ILogger<PlaidWebhookVerifier> logger,
        TimeProvider? time = null)
    {
        _plaid = plaid;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public async Task<bool> VerifyAsync(
        string body,
        string? jwt,
        CancellationToken ct = default)
    {
        try
        {
            var parts = jwt?.Split('.');
            if (parts is not { Length: 3 })
                return false;

            using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
            if (header.RootElement.GetProperty("alg").GetString() != "ES256" ||
                header.RootElement.GetProperty("kid").GetString() is not { } kid)
            {
                return false;
            }

            var key = await GetKeyAsync(kid, ct);
            if (key.ExpiredAt is not null || key.Curve != "P-256")
                return false;

            using var ecdsa = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = Base64UrlDecode(key.X), Y = Base64UrlDecode(key.Y) }
            });

            // JWS ES256 signatures are raw r||s, .NET's default format.
            if (!ecdsa.VerifyData(
                    Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"),
                    Base64UrlDecode(parts[2]),
                    HashAlgorithmName.SHA256))
            {
                return false;
            }

            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(
                payload.RootElement.GetProperty("iat").GetInt64());
            if (_time.GetUtcNow() - issuedAt > MaximumAge)
                return false;

            var expectedHash = payload.RootElement
                .GetProperty("request_body_sha256").GetString() ?? "";
            var actualHash = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(body)));

            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expectedHash),
                Encoding.ASCII.GetBytes(actualHash));
        }
        catch (Exception ex) when (ex is FormatException or JsonException
                                       or KeyNotFoundException or InvalidOperationException
                                       or CryptographicException)
        {
            _logger.LogWarning(ex, "Malformed Plaid webhook verification header.");
            return false;
        }
    }

    private async Task<PlaidWebhookKey> GetKeyAsync(string kid, CancellationToken ct)
    {
        if (Keys.TryGetValue(kid, out var cached))
            return cached;

        var key = await _plaid.GetWebhookVerificationKeyAsync(kid, ct);
        Keys[kid] = key;
        return key;
    }

    private static byte[] Base64UrlDecode(string value) =>
        Convert.FromBase64String(
            value.Replace('-', '+').Replace('_', '/')
                .PadRight(value.Length + (4 - value.Length % 4) % 4, '='));
}
