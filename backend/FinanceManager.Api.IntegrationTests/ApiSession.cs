using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Api.IntegrationTests;

// Drives the API the way the Angular app does: cookie auth plus a fresh
// CSRF token on every state-changing request.
internal sealed class ApiSession(HttpClient client)
{
    public HttpClient Client { get; } = client;

    public Task<HttpResponseMessage> GetAsync(string url) => Client.GetAsync(url);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null)
    {
        // Antiforgery tokens are bound to the signed-in user, so fetch one
        // per request rather than reusing a token from before login.
        var csrf = await Client.GetFromJsonAsync<JsonElement>("/api/v1/security/csrf");

        using var request = new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : JsonContent.Create(body)
        };
        request.Headers.Add("X-XSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await Client.SendAsync(request);
    }

    public Task<HttpResponseMessage> PostAsync(string url, object? body = null) =>
        SendAsync(HttpMethod.Post, url, body ?? new { });

    // Registers, confirms the email via the captured link, and signs in.
    public static async Task<ApiSession> SignUpAsync(ApiFactory factory, string email)
    {
        const string password = "Correct-Horse-Battery-9";
        var session = new ApiSession(factory.CreateHttpsClient());

        var register = await session.PostAsync("/api/v1/auth/register",
            new { firstName = "Test", lastName = "User", email, password });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var link = new Uri(factory.Emails.ConfirmationLinks[email]);
        var token = System.Web.HttpUtility.ParseQueryString(link.Query)["token"];
        var confirm = await session.PostAsync("/api/v1/auth/confirm-email", new { email, token });
        Assert.True(confirm.IsSuccessStatusCode, await confirm.Content.ReadAsStringAsync());

        var login = await session.PostAsync("/api/v1/auth/login", new { email, password });
        Assert.True(login.IsSuccessStatusCode, await login.Content.ReadAsStringAsync());

        return session;
    }
}
