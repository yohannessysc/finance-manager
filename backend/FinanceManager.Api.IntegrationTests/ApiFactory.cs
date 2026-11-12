using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Testcontainers.PostgreSql;

namespace FinanceManager.Api.IntegrationTests;

// Hosts the real API in its production configuration against a throwaway
// Postgres container (needs Docker). Shared by every test in the collection.
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private readonly string _certificatePath =
        Path.Combine(Path.GetTempPath(), $"fm-dp-{Guid.NewGuid():N}.pfx");

    public FakeEmailService Emails { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Production refuses to start without a key-encryption certificate.
        using var rsa = RSA.Create(2048);
        using var certificate = new CertificateRequest(
                "CN=Integration tests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        await File.WriteAllBytesAsync(_certificatePath, certificate.Export(X509ContentType.Pfx));

        // Migrate exactly as the deploy's migration job does.
        await using var db = new FinanceDbContext(
            new DbContextOptionsBuilder<FinanceDbContext>().UseNpgsql(ConnectionString).Options);
        await db.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        File.Delete(_certificatePath);
    }

    // HTTPS so the production Secure cookies round-trip.
    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("DataProtection:CertificatePath", _certificatePath);
        builder.UseSetting("Frontend:BaseUrl", "https://app.test");

        builder.ConfigureTestServices(services =>
        {
            // Background jobs run on timers; keep tests deterministic.
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);
        });
    }
}

public sealed class FakeEmailService : IEmailService
{
    public ConcurrentDictionary<string, string> ConfirmationLinks { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task SendEmailConfirmationAsync(string email, string confirmationUrl, CancellationToken ct = default)
    {
        ConfirmationLinks[email] = confirmationUrl;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken ct = default) => Task.CompletedTask;

    public Task SendEmailChangeAsync(string email, string confirmationUrl, CancellationToken ct = default) => Task.CompletedTask;

    public Task SendHouseholdInvitationAsync(string email, string householdName, string invitationUrl, CancellationToken ct = default) => Task.CompletedTask;

    public Task SendSavingsGoalMilestoneAsync(string email, string goalName, int milestonePercent, CancellationToken ct = default) => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
