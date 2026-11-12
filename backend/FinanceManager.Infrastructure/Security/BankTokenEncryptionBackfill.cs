using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Security;

// Encrypts access tokens stored before encryption existed.
// Idempotent, so it is safe to run on every startup.
public static class BankTokenEncryptionBackfill
{
    public static async Task RunAsync(
        IServiceProvider services,
        CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<IBankTokenProtector>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(BankTokenEncryptionBackfill));

        var connections = await db.BankConnections
            .Where(x => x.PlaidAccessToken != null &&
                        x.PlaidAccessToken.StartsWith("access-"))
            .ToListAsync(ct);

        if (connections.Count == 0)
            return;

        foreach (var connection in connections)
            connection.PlaidAccessToken = protector.Protect(connection.PlaidAccessToken!);

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Encrypted {Count} legacy bank access tokens.",
            connections.Count);
    }
}
