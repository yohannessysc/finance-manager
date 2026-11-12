using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services.Plaid;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace FinanceManager.Application.Tests;

public sealed class PlaidWebhookTests : IDisposable
{
    private const string Body = """{"webhook_type":"TRANSACTIONS","webhook_code":"SYNC_UPDATES_AVAILABLE","item_id":"item-1"}""";

    // Unique per test run: the verifier caches keys by kid process-wide.
    private readonly string _kid = Guid.NewGuid().ToString();
    private readonly ECDsa _signingKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly Mock<IPlaidService> _plaid = new();
    private readonly PlaidWebhookVerifier _verifier;

    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly Mock<IBankSyncQueue> _queue = new();
    private readonly Mock<IEntitlementService> _entitlements = new();
    private readonly BankConnection _bank;

    public PlaidWebhookTests()
    {
        var q = _signingKey.ExportParameters(includePrivateParameters: false).Q;
        _plaid.Setup(x => x.GetWebhookVerificationKeyAsync(_kid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlaidWebhookKey(_kid, "P-256", Base64Url(q.X!), Base64Url(q.Y!), null));
        _verifier = new PlaidWebhookVerifier(_plaid.Object, NullLogger<PlaidWebhookVerifier>.Instance);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "hook@example.com",
            Email = "hook@example.com",
            SecurityStamp = Guid.NewGuid().ToString()
        };
        _bank = new BankConnection
        {
            UserId = user.Id,
            PlaidItemId = "item-1",
            InstitutionName = "Test bank",
            InstitutionId = "ins_1",
            Status = (int)BankConnectionStatus.Connected
        };
        _db.Users.Add(user);
        _db.BankConnections.Add(_bank);
        _db.SaveChanges();

        _entitlements.Setup(x => x.GetPlanAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubscriptionPlan.Bank);
    }

    public void Dispose()
    {
        _signingKey.Dispose();
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ValidSignatureIsAccepted()
    {
        Assert.True(await _verifier.VerifyAsync(Body, Sign(Body)));
    }

    [Fact]
    public async Task TamperedBodyStaleTokenAndWrongKeyAreRejected()
    {
        Assert.False(await _verifier.VerifyAsync(Body.Replace("item-1", "item-2"), Sign(Body)));
        Assert.False(await _verifier.VerifyAsync(Body, Sign(Body, issuedAt: DateTimeOffset.UtcNow.AddMinutes(-10))));
        Assert.False(await _verifier.VerifyAsync(Body, null));
        Assert.False(await _verifier.VerifyAsync(Body, "not.a.jwt"));

        using var otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.False(await _verifier.VerifyAsync(Body, Sign(Body, signingKey: otherKey)));
    }

    [Fact]
    public async Task SyncUpdatesQueueASyncForEntitledConnections()
    {
        await CreateHandler().HandleAsync(Body);
        _queue.Verify(x => x.Enqueue(_bank.Id), Times.Once);
    }

    [Fact]
    public async Task LoginRequiredMarksTheConnectionForReconnect()
    {
        await CreateHandler().HandleAsync("""
            {"webhook_type":"ITEM","webhook_code":"ERROR","item_id":"item-1",
             "error":{"error_code":"ITEM_LOGIN_REQUIRED"}}
            """);

        var status = await _db.BankConnections.AsNoTracking()
            .Where(x => x.Id == _bank.Id).Select(x => x.Status).SingleAsync();
        Assert.Equal((int)BankConnectionStatus.ExpiredLink, status);
        _queue.Verify(x => x.Enqueue(It.IsAny<Guid>()), Times.Never);
    }

    private PlaidWebhookHandler CreateHandler() => new(
        _db, _queue.Object, _entitlements.Object, NullLogger<PlaidWebhookHandler>.Instance);

    private string Sign(string body, DateTimeOffset? issuedAt = null, ECDsa? signingKey = null)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid = _kid, typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = (issuedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds(),
            request_body_sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(body)))
        }));
        var signature = (signingKey ?? _signingKey).SignData(
            Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256);
        return $"{header}.{payload}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
