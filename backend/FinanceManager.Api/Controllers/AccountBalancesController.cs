
using FinanceManager.Api.Filters;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/account-balances")]
public class AccountBalancesController : ControllerBase
{
    private readonly IAccountBalanceService _balanceService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AccountBalancesController(
        IAccountBalanceService balanceService,
        UserManager<ApplicationUser> userManager)
    {
        _balanceService = balanceService;
        _userManager = userManager;
    }

    private Guid? GetCurrentUserId()
    {
        var value = _userManager.GetUserId(User);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    // GET /api/v1/account-balances
    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var balances = await _balanceService.GetAllAsync(
            userId.Value,
            ct);

        return Ok(balances);
    }

    // GET /api/v1/account-balances/{accountId}
    [HttpGet("{accountId:guid}")]
    public async Task<IActionResult> GetByAccountId(
        Guid accountId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var balance = await _balanceService.GetByAccountIdAsync(
            accountId,
            userId.Value,
            ct);

        return balance is null
            ? NotFound()
            : Ok(balance);
    }
}
