using System.Text.Json;

using FinanceManager.Infrastructure.Services.Plaid;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

// Called by Plaid, not the browser: authenticated by Plaid's signed
// Plaid-Verification header instead of a session cookie, so no CSRF filter.
[ApiController]
[AllowAnonymous]
[Route("api/v1/bank/plaid/webhook")]
public sealed class PlaidWebhookController : ControllerBase
{
    private readonly PlaidWebhookVerifier _verifier;
    private readonly PlaidWebhookHandler _handler;
    private readonly ILogger<PlaidWebhookController> _logger;

    public PlaidWebhookController(
        PlaidWebhookVerifier verifier,
        PlaidWebhookHandler handler,
        ILogger<PlaidWebhookController> logger)
    {
        _verifier = verifier;
        _handler = handler;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // The signature covers the exact bytes, so read the raw body.
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        if (!await _verifier.VerifyAsync(body, Request.Headers["Plaid-Verification"], ct))
        {
            _logger.LogWarning("Rejected Plaid webhook with an invalid signature.");
            return BadRequest();
        }

        try
        {
            await _handler.HandleAsync(body, ct);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Signed but not a shape we understand: acknowledge so Plaid
            // does not retry it forever.
            _logger.LogWarning(ex, "Ignored an unrecognised Plaid webhook.");
        }

        return Ok();
    }
}
