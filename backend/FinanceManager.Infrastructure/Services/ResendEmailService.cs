
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Encodings.Web;
using FinanceManager.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services;

public sealed class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(
        HttpClient http,
        IConfiguration configuration,
        ILogger<ResendEmailService> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailConfirmationAsync(
        string email,
        string confirmationUrl,
        CancellationToken ct = default)
    {
        var apiKey = _configuration["Resend:ApiKey"];
        var from = _configuration["Resend:From"];

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException(
                "Resend email configuration is missing.");
        }

        var safeUrl = HtmlEncoder.Default.Encode(confirmationUrl);

        var html = $"""
            <!DOCTYPE html>
            <html lang="en">
              <body style="margin:0;padding:40px 15px;
                           background:#f4f8f7;
                           font-family:Arial,sans-serif;">
                <div style="max-width:520px;margin:auto;
                            background:white;border-radius:18px;
                            padding:42px 32px;">
                  <div style="font-weight:800;font-size:19px;
                              color:#153c32;">
                    <span style="background:#41c49a;color:white;
                                 padding:10px;border-radius:9px;">F</span>
                    &nbsp; Finance Manager
                  </div>

                  <h1 style="color:#162c28;margin-top:42px;">
                    Verify your email address
                  </h1>

                  <p style="color:#637771;line-height:1.8;">
                    Welcome to Finance Manager.
                    Please confirm your email address to finish
                    setting up your account.
                  </p>

                  <a href="{safeUrl}"
                     style="display:inline-block;margin:20px 0;
                            padding:15px 25px;
                            background:#13866b;color:#ffffff;
                            border-radius:10px;
                            text-decoration:none;font-weight:bold;">
                    Verify email address
                  </a>

                  <p style="color:#637771;font-size:13px;
                            line-height:1.7;">
                    If you didn't create this account,
                    you can ignore this message.
                  </p>

                  <p style="color:#9ba9a5;font-size:11px;
                            margin-top:30px;">
                    Finance Manager · Account security
                  </p>
                </div>
              </body>
            </html>
            """;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.resend.com/emails");

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        request.Content = JsonContent.Create(new
        {
            from,
            to = new[] { email },
            subject = "Verify your Finance Manager email",
            html
        });

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Resend email API returned status {StatusCode}.",
                (int)response.StatusCode);

            throw new HttpRequestException(
                "Verification email delivery failed.",
                null,
                response.StatusCode);
        }

        _logger.LogInformation(
            "Verification email accepted by email provider.");
    }


    public async Task SendPasswordResetAsync(
        string email,
        string resetUrl,
        CancellationToken ct = default)
    {
        var apiKey = _configuration["Resend:ApiKey"];
        var from = _configuration["Resend:From"];

        if (string.IsNullOrWhiteSpace(apiKey) ||
            string.IsNullOrWhiteSpace(from))
        {
            throw new InvalidOperationException(
                "Resend email configuration is missing.");
        }

        var safeUrl = System.Text.Encodings.Web.HtmlEncoder
            .Default.Encode(resetUrl);

        var html = $"""
        <div style="font-family:Arial,sans-serif;
                    max-width:520px;margin:40px auto;
                    padding:32px;border-radius:16px;
                    background:#ffffff;">
          <h2 style="color:#123b32;">
            Reset your Finance Manager password
          </h2>

          <p style="color:#64748b;line-height:1.7;">
            We received a request to reset your password.
            Click below to choose a new password.
          </p>

          <a href="{safeUrl}"
             style="display:inline-block;margin:24px 0;
                    background:#13866b;color:#ffffff;
                    padding:14px 24px;border-radius:9px;
                    text-decoration:none;font-weight:bold;">
            Reset password
          </a>

          <p style="color:#64748b;font-size:13px;">
            If you didn't request this, you can ignore
            this email. Your password remains unchanged.
          </p>
        </div>
        """;

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.resend.com/emails");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", apiKey);

        request.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            from,
            to = new[] { email },
            subject = "Reset your Finance Manager password",
            html
        });

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Password reset email request failed: {StatusCode}",
                (int)response.StatusCode);

            throw new HttpRequestException(
                "Password reset email delivery failed.",
                null,
                response.StatusCode);
        }
    }

}
