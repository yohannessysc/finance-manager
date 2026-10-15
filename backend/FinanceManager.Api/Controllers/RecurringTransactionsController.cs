
using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.RecurringTransactions;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/recurring-transactions")]
public class RecurringTransactionsController : ControllerBase
{
    private readonly IRecurringTransactionService _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public RecurringTransactionsController(
        IRecurringTransactionService service,
        UserManager<ApplicationUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    private Guid? CurrentUserId()
    {
        var value = _userManager.GetUserId(User);

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        return Ok(await _service.GetAllAsync(userId.Value, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        var result = await _service.GetByIdAsync(
            id, userId.Value, ct);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecurringTransactionRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _service.CreateAsync(
                userId.Value, request, ct);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRecurringTransactionRequest request,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _service.UpdateAsync(
                id, userId.Value, request, ct);

            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/pause")]
    public Task<IActionResult> Pause(Guid id, CancellationToken ct)
        => ChangeStatus(id, "pause", ct);

    [HttpPost("{id:guid}/resume")]
    public Task<IActionResult> Resume(Guid id, CancellationToken ct)
        => ChangeStatus(id, "resume", ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ChangeStatus(id, "cancel", ct);

    private async Task<IActionResult> ChangeStatus(
        Guid id,
        string operation,
        CancellationToken ct)
    {
        var userId = CurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            bool success = operation switch
            {
                "pause" => await _service.PauseAsync(
                    id, userId.Value, ct),

                "resume" => await _service.ResumeAsync(
                    id, userId.Value, ct),

                "cancel" => await _service.CancelAsync(
                    id, userId.Value, ct),

                _ => false
            };

            return success ? NoContent() : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
