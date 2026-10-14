
using FinanceManager.Application.DTOs.Accounts;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Api.Filters;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;


[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/accounts")]
public class FinancialAccountsController : ControllerBase
{
    private readonly IFinancialAccountService _accountService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FinancialAccountsController(
        IFinancialAccountService accountService,
        UserManager<ApplicationUser> userManager)
    {
        _accountService = accountService;
        _userManager = userManager;
    }

    private Guid? GetCurrentUserId()
    {
        var userId = _userManager.GetUserId(User);

        return Guid.TryParse(userId, out var id)
            ? id
            : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var accounts = await _accountService.GetAllAsync(
            userId.Value, ct);

        return Ok(accounts);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccount(
        Guid id,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var account = await _accountService.GetByIdAsync(
            id, userId.Value, ct);

        return account is null
            ? NotFound()
            : Ok(account);
    }

    [HttpPost]
    public async Task<IActionResult> CreateAccount(
        [FromBody] CreateAccountRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var account = await _accountService.CreateAsync(
                userId.Value, request, ct);

            return CreatedAtAction(
                nameof(GetAccount),
                new { id = account.Id },
                account);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAccount(
        Guid id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var account = await _accountService.UpdateAsync(
                id, userId.Value, request, ct);

            return account is null
                ? NotFound()
                : Ok(account);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAccount(
        Guid id,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var deleted = await _accountService.DeleteAsync(
            id, userId.Value, ct);

        return deleted
            ? NoContent()
            : NotFound();
    }
}
