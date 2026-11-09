using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.Budgets;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/budgets")]
public sealed class BudgetsController : ControllerBase
{
    private readonly IBudgetService _budgets;
    private readonly UserManager<ApplicationUser> _users;

    public BudgetsController(
        IBudgetService budgets,
        UserManager<ApplicationUser> users)
    {
        _budgets = budgets;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();

        return userId is null
            ? Unauthorized()
            : Ok(await _budgets.GetAllAsync(userId.Value, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] BudgetUpsertRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            return Ok(await _budgets.CreateAsync(userId.Value, request, ct));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] BudgetUpsertRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var budget = await _budgets.UpdateAsync(
                id,
                userId.Value,
                request,
                ct);

            return budget is null ? NotFound() : Ok(budget);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        return await _budgets.DeleteAsync(id, userId.Value, ct)
            ? NoContent()
            : NotFound();
    }

    private Guid? GetUserId()
    {
        return Guid.TryParse(_users.GetUserId(User), out var id)
            ? id
            : null;
    }
}
