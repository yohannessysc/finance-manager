using FinanceManager.Application.Interfaces;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

// Called by Stripe, not the browser: authenticated by the Stripe
// signature instead of a session cookie, so no CSRF filter.
[ApiController]
[AllowAnonymous]
[Route("api/v1/billing/webhook")]
public sealed class BillingWebhookController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly ILogger<BillingWebhookController> _logger;

    public BillingWebhookController(
        IBillingService billing,
        ILogger<BillingWebhookController> logger)
    {
        _billing = billing;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // The signature covers the exact bytes, so read the raw body.
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        try
        {
            await _billing.HandleWebhookAsync(
                payload,
                Request.Headers["Stripe-Signature"].ToString(),
                ct);
            return Ok();
        }
        catch (BillingWebhookException ex)
        {
            _logger.LogWarning(ex, "Rejected Stripe webhook.");
            return BadRequest();
        }
        catch (BillingNotConfiguredException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
