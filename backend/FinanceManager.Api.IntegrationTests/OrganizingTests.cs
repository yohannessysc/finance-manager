using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Api.IntegrationTests;

// Receipts, splits, subcategories and reports against real Postgres.
[Collection(ApiCollection.Name)]
public sealed class OrganizingTests(ApiFactory factory)
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];

    private static async Task<string> IdOf(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<(string Account, string Transaction, string Food, string Groceries)> SeedAsync(ApiSession session)
    {
        var account = await IdOf(await session.PostAsync("/api/v1/accounts",
            new { name = "Chequing", accountType = "Chequing", openingBalance = 0 }));
        var food = await IdOf(await session.PostAsync("/api/v1/categories", new { name = "Eating", type = 2 }));
        var groceries = await IdOf(await session.PostAsync("/api/v1/categories",
            new { name = "Supermarket", type = 2, parentCategoryId = food }));
        var transaction = await IdOf(await session.PostAsync("/api/v1/transactions", new
        {
            financialAccountId = account,
            categoryId = groceries,
            type = 2,
            amount = 100,
            description = "Costco",
            transactionDateUtc = DateTime.UtcNow.AddMinutes(-5)
        }));
        return (account, transaction, food, groceries);
    }

    [Fact]
    public async Task ReceiptsAreVerifiedServedSafelyAndPrivate()
    {
        var owner = await ApiSession.SignUpAsync(factory, "receipts@example.com");
        var (_, transaction, _, _) = await SeedAsync(owner);
        var url = $"/api/v1/transactions/{transaction}/attachments";

        var receipt = await IdOf(await owner.PostFileAsync(url, "costco.jpg", Jpeg));

        // A renamed HTML file is rejected by its contents.
        var fake = await owner.PostFileAsync(url, "evil.jpg", "<html><script>alert(1)</script>"u8.ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, fake.StatusCode);

        var download = await owner.GetAsync($"{url}/{receipt}");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/jpeg", download.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("sandbox", download.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal(Jpeg, await download.Content.ReadAsByteArrayAsync());

        var listed = await owner.Client.GetFromJsonAsync<JsonElement>("/api/v1/transactions");
        Assert.Equal(1, listed[0].GetProperty("attachmentCount").GetInt32());

        var stranger = await ApiSession.SignUpAsync(factory, "stranger@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"{url}/{receipt}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostFileAsync(url, "x.jpg", Jpeg)).StatusCode);
    }

    [Fact]
    public async Task SplitsShowInReportsAndAccountsWithAllOfItCanBeDeleted()
    {
        var session = await ApiSession.SignUpAsync(factory, "organized@example.com");
        var (_, transaction, food, groceries) = await SeedAsync(session);
        var household = await IdOf(await session.PostAsync("/api/v1/categories", new { name = "Home supplies", type = 2 }));

        var split = await session.SendAsync(HttpMethod.Put, $"/api/v1/transactions/{transaction}/splits", new
        {
            lines = new object[]
            {
                new { categoryId = groceries, amount = 70, note = (string?)null },
                new { categoryId = household, amount = 30, note = "Soap" }
            }
        });
        Assert.True(split.IsSuccessStatusCode, await split.Content.ReadAsStringAsync());
        await session.PostFileAsync($"/api/v1/transactions/{transaction}/attachments", "r.jpg", Jpeg);

        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var report = await session.Client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/reports/categories?from={month}&to={month}");
        var rows = report.GetProperty("categories").EnumerateArray().ToList();
        Assert.Equal(70m, rows.Single(r => r.GetProperty("categoryId").GetString() == food).GetProperty("total").GetDecimal());
        Assert.Equal(30m, rows.Single(r => r.GetProperty("categoryId").GetString() == household).GetProperty("total").GetDecimal());

        var deleted = await session.PostAsync("/api/v1/account-privacy/delete-account",
            new { currentPassword = "Correct-Horse-Battery-9", confirmation = "DELETE" });
        Assert.True(deleted.IsSuccessStatusCode, await deleted.Content.ReadAsStringAsync());
    }
}
