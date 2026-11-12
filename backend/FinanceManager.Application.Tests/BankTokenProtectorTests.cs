using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Security;
using FinanceManager.Application.Interfaces;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Application.Tests;

public sealed class BankTokenProtectorTests
{
    private const string PlainToken = "access-sandbox-1b2c3d4e-0000-4000-8000-123456789abc";

    private readonly BankTokenProtector _protector =
        new(new EphemeralDataProtectionProvider());

    [Fact]
    public void ProtectedTokensRoundTripAndHideThePlaintext()
    {
        var stored = _protector.Protect(PlainToken);

        Assert.DoesNotContain("access-", stored);
        Assert.False(_protector.IsLegacyPlaintext(stored));
        Assert.Equal(PlainToken, _protector.Unprotect(stored));
    }

    [Fact]
    public void LegacyPlaintextTokensAreStillReadable()
    {
        Assert.True(_protector.IsLegacyPlaintext(PlainToken));
        Assert.Equal(PlainToken, _protector.Unprotect(PlainToken));
    }

    [Fact]
    public async Task BackfillEncryptsLegacyTokensOnce()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection()
            .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
            .AddSingleton<IBankTokenProtector>(_protector)
            .AddDbContext<FinanceDbContext>(x => x.UseSqlite(connection))
            .BuildServiceProvider();

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
            db.Database.EnsureCreated();

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = "bank@example.com",
                Email = "bank@example.com",
                SecurityStamp = Guid.NewGuid().ToString()
            };
            db.Users.Add(user);
            db.BankConnections.Add(new BankConnection
            {
                UserId = user.Id,
                BankProvider = "Plaid",
                InstitutionName = "Test bank",
                InstitutionId = "ins_1",
                PlaidAccessToken = PlainToken
            });
            await db.SaveChangesAsync();
        }

        await BankTokenEncryptionBackfill.RunAsync(services);
        var firstPass = await ReadTokenAsync(services);
        await BankTokenEncryptionBackfill.RunAsync(services);

        Assert.False(_protector.IsLegacyPlaintext(firstPass));
        Assert.Equal(firstPass, await ReadTokenAsync(services));
        Assert.Equal(PlainToken, _protector.Unprotect(firstPass));
    }

    private static async Task<string> ReadTokenAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<FinanceDbContext>()
            .BankConnections
            .Select(x => x.PlaidAccessToken!)
            .SingleAsync();
    }
}
