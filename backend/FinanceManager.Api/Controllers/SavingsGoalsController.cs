using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.SavingsGoals;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/savings-goals")]
public sealed class SavingsGoalsController : ControllerBase
{
    private readonly ISavingsGoalService _goals;
    private readonly UserManager<ApplicationUser> _users;

    public SavingsGoalsController(
        ISavingsGoalService goals,
        UserManager<ApplicationUser> users)
    {
        _goals = goals;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetUserId();
        return userId is null
            ? Unauthorized()
            : Ok(await _goals.GetAllAsync(userId.Value, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] SavingsGoalUpsertRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            return Ok(await _goals.CreateAsync(userId.Value, request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] SavingsGoalUpdateRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var goal = await _goals.UpdateAsync(id, userId.Value, request, ct);
            return goal is null ? NotFound() : Ok(goal);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            return await _goals.DeleteAsync(id, userId.Value, ct)
                ? NoContent()
                : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("{id:guid}/contributions")]
    public async Task<IActionResult> GetContributions(
        Guid id,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        var contributions = await _goals.GetContributionsAsync(
            id,
            userId.Value,
            ct);

        return contributions is null ? NotFound() : Ok(contributions);
    }

    [HttpPost("{id:guid}/contributions")]
    public async Task<IActionResult> AddContribution(
        Guid id,
        [FromBody] SavingsGoalContributionRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var goal = await _goals.AddContributionAsync(
                id,
                userId.Value,
                request,
                ct);

            return goal is null ? NotFound() : Ok(goal);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpDelete("{id:guid}/contributions/{contributionId:guid}")]
    public async Task<IActionResult> DeleteContribution(
        Guid id,
        Guid contributionId,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            return await _goals.DeleteContributionAsync(
                id,
                contributionId,
                userId.Value,
                ct)
                ? NoContent()
                : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    private Guid? GetUserId()
    {
        return Guid.TryParse(_users.GetUserId(User), out var id)
            ? id
            : null;
    }
}
