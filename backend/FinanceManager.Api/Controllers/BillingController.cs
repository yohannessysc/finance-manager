using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.Billing;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/billing")]
public sealed class BillingController : ControllerBase
{
    private readonly IBillingService _billing;
    private readonly UserManager<ApplicationUser> _users;

    public BillingController(
        IBillingService billing,
        UserManager<ApplicationUser> users)
    {
        _billing = billing;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : Ok(await _billing.GetStatusAsync(userId.Value, ct));
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout(
        [FromBody] CheckoutRequest request,
        CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);

        if (user?.Email is null)
            return Unauthorized();

        return await RedirectAsync(() =>
            _billing.CreateCheckoutSessionAsync(user.Id, user.Email, request, ct));
    }

    [HttpPost("portal")]
    public async Task<IActionResult> Portal(CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        return await RedirectAsync(() =>
            _billing.CreatePortalSessionAsync(userId.Value, ct));
    }

    private async Task<IActionResult> RedirectAsync(
        Func<Task<BillingRedirectResponse>> create)
    {
        try
        {
            return Ok(await create());
        }
        catch (BillingNotConfiguredException ex)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    private Guid? GetUserId()
    {
        return Guid.TryParse(_users.GetUserId(User), out var id)
            ? id
            : null;
    }
}
