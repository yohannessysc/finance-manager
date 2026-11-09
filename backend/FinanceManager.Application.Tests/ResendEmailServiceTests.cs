using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using FinanceManager.Infrastructure.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceManager.Application.Tests;

public sealed class ResendEmailServiceTests
{
    [Fact]
    public async Task SendPasswordResetAsync_PostsRecipientAndResetLink()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        await service.SendPasswordResetAsync(
            "user@example.com",
            "https://finance.example/reset-password?token=reset-token");

        Assert.Equal(
            "Bearer",
            handler.Authorization?.Scheme);
        Assert.Equal(
            "test-api-key",
            handler.Authorization?.Parameter);
        Assert.Equal(
            "https://api.resend.com/emails",
            handler.RequestUri?.ToString());

        using var body = JsonDocument.Parse(handler.Body!);

        Assert.Equal(
            "Finance Manager <accounts@example.com>",
            body.RootElement.GetProperty("from").GetString());
        Assert.Equal(
            "user@example.com",
            body.RootElement.GetProperty("to")[0].GetString());
        Assert.Contains(
            "https://finance.example/reset-password?token=reset-token",
            body.RootElement.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendEmailChangeAsync_UsesNewAddressAndConfirmationLink()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        await service.SendEmailChangeAsync(
            "new@example.com",
            "https://finance.example/confirm-email-change?token=confirm-token");

        using var body = JsonDocument.Parse(handler.Body!);

        Assert.Equal(
            "new@example.com",
            body.RootElement.GetProperty("to")[0].GetString());
        Assert.Contains(
            "https://finance.example/confirm-email-change?token=confirm-token",
            body.RootElement.GetProperty("html").GetString());
    }

    [Fact]
    public async Task SendPasswordResetAsync_ThrowsWhenProviderRejectsRequest()
    {
        var handler = new CapturingHandler(HttpStatusCode.ServiceUnavailable);
        var service = CreateService(handler);

        var error = await Assert.ThrowsAsync<HttpRequestException>(
            () => service.SendPasswordResetAsync(
                "user@example.com",
                "https://finance.example/reset-password"));

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            error.StatusCode);
    }

    private static ResendEmailService CreateService(
        CapturingHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resend:ApiKey"] = "test-api-key",
                ["Resend:From"] = "Finance Manager <accounts@example.com>"
            })
            .Build();

        return new ResendEmailService(
            new HttpClient(handler),
            configuration,
            NullLogger<ResendEmailService>.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public CapturingHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public AuthenticationHeaderValue? Authorization { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization;
            RequestUri = request.RequestUri;
            Body = await request.Content!
                .ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode);
        }
    }
}
