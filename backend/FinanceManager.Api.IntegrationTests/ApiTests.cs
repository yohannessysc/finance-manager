using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FinanceManager.Application.Interfaces;

using Microsoft.Extensions.DependencyInjection;

namespace FinanceManager.Api.IntegrationTests;

// Critical paths against the real pipeline and Postgres.
[Collection(ApiCollection.Name)]
public sealed class ApiTests(ApiFactory factory)
{
    [Fact]
    public async Task HealthChecksAndSecurityHeaders()
    {
        var client = factory.CreateHttpsClient();

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("Healthy", await ready.Content.ReadAsStringAsync());

        var api = await client.GetAsync("/api/v1/billing");
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Equal("nosniff", api.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", api.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task SignUpConfirmSignInAndSignOut()
    {
        var session = await ApiSession.SignUpAsync(factory, "flow@example.com");

        var me = await session.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var logout = await session.PostAsync("/api/v1/auth/logout");
        Assert.True(logout.IsSuccessStatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task StateChangingRequestsNeedACsrfToken()
    {
        var session = await ApiSession.SignUpAsync(factory, "csrf@example.com");

        var withoutToken = await session.Client.PostAsJsonAsync("/api/v1/accounts",
            new { name = "Chequing", accountType = "Chequing", openingBalance = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
    }

    [Fact]
    public async Task FreePlanCannotLinkBanksButCanTrackSavingsGoals()
    {
        var session = await ApiSession.SignUpAsync(factory, "saver@example.com");

        var billing = await session.Client.GetFromJsonAsync<JsonElement>("/api/v1/billing");
        Assert.Equal(0, billing.GetProperty("plan").GetInt32());

        var link = await session.PostAsync("/api/v1/bank/plaid/link-token", new { });
        Assert.Equal(HttpStatusCode.Forbidden, link.StatusCode);
        var error = await link.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PLAN_REQUIRED", error.GetProperty("code").GetString());

        var account = await session.PostAsync("/api/v1/accounts",
            new { name = "Savings", accountType = "Savings", openingBalance = 0 });
        Assert.True(account.IsSuccessStatusCode, await account.Content.ReadAsStringAsync());

        var goal = await (await session.PostAsync("/api/v1/savings-goals", new
        {
            name = "Trip",
            currency = "CAD",
            targetAmount = 1000,
            targetDateUtc = DateTime.UtcNow.AddMonths(10)
        })).Content.ReadFromJsonAsync<JsonElement>();

        var contributed = await session.PostAsync(
            $"/api/v1/savings-goals/{goal.GetProperty("id").GetString()}/contributions",
            new { amount = 250, contributionDateUtc = DateTime.UtcNow, note = "First" });
        Assert.True(contributed.IsSuccessStatusCode, await contributed.Content.ReadAsStringAsync());

        var updated = await contributed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(250, updated.GetProperty("amountSaved").GetDecimal());
        Assert.Equal(25, updated.GetProperty("progressPercent").GetDecimal());
    }

    [Fact]
    public async Task UnsignedWebhooksAreRejected()
    {
        var client = factory.CreateHttpsClient();

        var plaid = await client.PostAsync("/api/v1/bank/plaid/webhook",
            JsonContent.Create(new { webhook_type = "TRANSACTIONS", webhook_code = "SYNC_UPDATES_AVAILABLE", item_id = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, plaid.StatusCode);

        // Stripe is not configured in tests, so billing webhooks are refused.
        var stripe = await client.PostAsync("/api/v1/billing/webhook", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, stripe.StatusCode);
    }

    [Fact]
    public async Task JobLockAllowsOneHolderAtATime()
    {
        var jobLock = factory.Services.GetRequiredService<IJobLock>();

        await using (var first = await jobLock.TryAcquireAsync("integration-test"))
        {
            Assert.NotNull(first);
            Assert.Null(await jobLock.TryAcquireAsync("integration-test"));
            await using var other = await jobLock.TryAcquireAsync("other-job");
            Assert.NotNull(other);
        }

        await using var again = await jobLock.TryAcquireAsync("integration-test");
        Assert.NotNull(again);
    }
}
