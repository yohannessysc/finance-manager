using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace FinanceManager.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class TwoFactorTests(ApiFactory factory)
{
    [Fact]
    public async Task AuthenticatorAndRecoveryCodesGuardSignIn()
    {
        const string email = "mfa@example.com";
        var session = await ApiSession.SignUpAsync(factory, email);

        var setup = await (await session.PostAsync("/api/v1/account/two-factor/setup"))
            .Content.ReadFromJsonAsync<JsonElement>();
        var key = setup.GetProperty("sharedKey").GetString()!.Replace(" ", "");

        var enable = await session.PostAsync("/api/v1/account/two-factor/enable", new { code = Totp(key) });
        Assert.True(enable.IsSuccessStatusCode, await enable.Content.ReadAsStringAsync());
        var recoveryCodes = (await enable.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("recoveryCodes").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Equal(10, recoveryCodes.Count);

        // Enabling rotated the security stamp; this session must survive it.
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/auth/me")).StatusCode);
        await session.PostAsync("/api/v1/auth/logout");

        // The password alone no longer signs in.
        var login = await session.PostAsync("/api/v1/auth/login",
            new { email, password = "Correct-Horse-Battery-9" });
        Assert.True((await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requiresTwoFactor").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/auth/me")).StatusCode);

        var wrong = await session.PostAsync("/api/v1/auth/login/two-factor", new { code = "000000" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);

        // Identity rejects reusing the code that enabled MFA (replay
        // protection), so wait for the authenticator's next code.
        await WaitForNextTimeStepAsync();
        var right = await session.PostAsync("/api/v1/auth/login/two-factor", new { code = Totp(key) });
        Assert.True(right.IsSuccessStatusCode, await right.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/auth/me")).StatusCode);

        // A recovery code works once.
        await session.PostAsync("/api/v1/auth/logout");
        await session.PostAsync("/api/v1/auth/login", new { email, password = "Correct-Horse-Battery-9" });
        // Typed loosely (lower case, no dash) as users often do.
        var recovery = await session.PostAsync("/api/v1/auth/login/two-factor",
            new { code = recoveryCodes[0].Replace("-", "").ToLowerInvariant(), isRecoveryCode = true });
        Assert.True(recovery.IsSuccessStatusCode, await recovery.Content.ReadAsStringAsync());

        await session.PostAsync("/api/v1/auth/logout");
        await session.PostAsync("/api/v1/auth/login", new { email, password = "Correct-Horse-Battery-9" });
        var reused = await session.PostAsync("/api/v1/auth/login/two-factor",
            new { code = recoveryCodes[0], isRecoveryCode = true });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    private static Task WaitForNextTimeStepAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return Task.Delay(TimeSpan.FromMilliseconds(30_000 - now % 30_000 + 250));
    }

    // RFC 6238 TOTP (SHA-1, 30 s, 6 digits), as authenticator apps compute it.
    private static string Totp(string base32Key)
    {
        var counter = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counter);

        var hash = HMACSHA1.HashData(Base32Decode(base32Key), counter);
        var offset = hash[^1] & 0x0F;
        var value = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) |
                    (hash[offset + 2] << 8) | hash[offset + 3];
        return (value % 1_000_000).ToString("D6");
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in input.ToUpperInvariant().TrimEnd('='))
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return [.. bytes];
    }
}
