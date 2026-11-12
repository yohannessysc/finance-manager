using FinanceManager.Application.DTOs.Billing;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Billing;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

namespace FinanceManager.Application.Tests;

public sealed class BillingServiceTests : IDisposable
{
    private const string BankMonthly = "price_bank_monthly";
    private const string PremiumMonthly = "price_premium_monthly";

    private readonly SqliteConnection _connection;
    private readonly FinanceDbContext _db;
    private readonly Mock<IStripeGateway> _stripe = new();
    private readonly BillingOptions _options = new()
    {
        StripeSecretKey = "sk_test",
        StripeWebhookSecret = "whsec_test",
        Bank = new() { MonthlyPriceId = BankMonthly },
        Premium = new() { MonthlyPriceId = PremiumMonthly }
    };
    private readonly Guid _userId = Guid.NewGuid();

    public BillingServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _db = new FinanceDbContext(new DbContextOptionsBuilder<FinanceDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new ApplicationUser
        {
            Id = _userId,
            UserName = "payer@example.com",
            Email = "payer@example.com",
            SecurityStamp = Guid.NewGuid().ToString()
        });
        _db.SaveChanges();

        _stripe.Setup(x => x.CreateCustomerAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("cus_1");
        _stripe.Setup(x => x.CreateCheckoutSessionAsync(
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://checkout.stripe.test/session");
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task NewUsersAreOnTheFreePlan()
    {
        Assert.Equal(SubscriptionPlan.Free, await CreateService().GetPlanAsync(_userId));
    }

    [Fact]
    public async Task CheckoutCreatesOneCustomerAndUsesTheMappedPrice()
    {
        var service = CreateService();

        var result = await service.CreateCheckoutSessionAsync(
            _userId, "payer@example.com", new CheckoutRequest(SubscriptionPlan.Bank));
        await service.CreateCheckoutSessionAsync(
            _userId, "payer@example.com", new CheckoutRequest(SubscriptionPlan.Bank));

        Assert.Equal("https://checkout.stripe.test/session", result.Url);
        _stripe.Verify(x => x.CreateCustomerAsync(_userId, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _stripe.Verify(x => x.CreateCheckoutSessionAsync(
            "cus_1", _userId, BankMonthly, 0,
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCheckoutSessionAsync(
            _userId, "payer@example.com",
            new CheckoutRequest(SubscriptionPlan.Bank, BillingInterval.Yearly)));
    }

    [Fact]
    public async Task WebhooksGrantAndRevokeThePlan()
    {
        var service = CreateService();
        await service.CreateCheckoutSessionAsync(
            _userId, "payer@example.com", new CheckoutRequest(SubscriptionPlan.Premium));

        await DeliverAsync(service, Snapshot("sub_1", "active", PremiumMonthly));
        Assert.Equal(SubscriptionPlan.Premium, await service.GetPlanAsync(_userId));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCheckoutSessionAsync(
            _userId, "payer@example.com", new CheckoutRequest(SubscriptionPlan.Bank)));

        await DeliverAsync(service, Snapshot("sub_1", "canceled", PremiumMonthly));
        Assert.Equal(SubscriptionPlan.Free, await service.GetPlanAsync(_userId));
    }

    [Fact]
    public async Task StaleEventsForAnEndedSubscriptionAreIgnored()
    {
        var service = CreateService();
        await service.CreateCheckoutSessionAsync(
            _userId, "payer@example.com", new CheckoutRequest(SubscriptionPlan.Bank));

        await DeliverAsync(service, Snapshot("sub_new", "active", BankMonthly));
        await DeliverAsync(service, Snapshot("sub_old", "canceled", PremiumMonthly));

        Assert.Equal(SubscriptionPlan.Bank, await service.GetPlanAsync(_userId));
    }

    [Fact]
    public async Task DevelopmentDefaultPlanAppliesWithoutASubscription()
    {
        _options.DefaultPlan = SubscriptionPlan.Premium;
        Assert.Equal(SubscriptionPlan.Premium, await CreateService().GetPlanAsync(_userId));
    }

    private static StripeSubscriptionSnapshot Snapshot(string id, string status, string priceId) =>
        new(id, "cus_1", status, priceId, DateTime.UtcNow.AddMonths(1), false);

    private async Task DeliverAsync(BillingService service, StripeSubscriptionSnapshot snapshot)
    {
        _stripe.Setup(x => x.ParseWebhook("payload", "sig"))
            .Returns(new StripeWebhookEvent("customer.subscription.updated", snapshot.Id));
        _stripe.Setup(x => x.GetSubscriptionAsync(snapshot.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        await service.HandleWebhookAsync("payload", "sig");
    }

    private BillingService CreateService() => new(
        _db,
        _stripe.Object,
        Options.Create(_options),
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://app.test"
            })
            .Build(),
        NullLogger<BillingService>.Instance);
}
