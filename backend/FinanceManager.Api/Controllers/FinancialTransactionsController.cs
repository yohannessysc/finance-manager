
using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.Transactions;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/transactions")]
public class FinancialTransactionsController : ControllerBase
{
    private readonly IFinancialTransactionService _transactionService;
    private readonly UserManager<ApplicationUser> _userManager;

    public FinancialTransactionsController(
        IFinancialTransactionService transactionService,
        UserManager<ApplicationUser> userManager)
    {
        _transactionService = transactionService;
        _userManager = userManager;
    }

    private Guid? GetCurrentUserId()
    {
        var userId = _userManager.GetUserId(User);

        return Guid.TryParse(userId, out var id)
            ? id
            : null;
    }

    // GET /api/v1/transactions
    [HttpGet]
    public async Task<IActionResult> GetTransactions(
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var transactions = await _transactionService.GetAllAsync(
            userId.Value, ct);

        return Ok(transactions);
    }

    // GET /api/v1/transactions/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTransaction(
        Guid id,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var transaction = await _transactionService.GetByIdAsync(
            id, userId.Value, ct);

        return transaction is null
            ? NotFound()
            : Ok(transaction);
    }

    // POST /api/v1/transactions
    [HttpPost]
    public async Task<IActionResult> CreateTransaction(
        [FromBody] CreateTransactionRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var transaction = await _transactionService.CreateAsync(
                userId.Value, request, ct);

            return CreatedAtAction(
                nameof(GetTransaction),
                new { id = transaction.Id },
                transaction);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT /api/v1/transactions/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTransaction(
        Guid id,
        [FromBody] UpdateTransactionRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var transaction = await _transactionService.UpdateAsync(
                id, userId.Value, request, ct);

            return transaction is null
                ? NotFound()
                : Ok(transaction);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // DELETE /api/v1/transactions/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTransaction(
        Guid id,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var deleted = await _transactionService.DeleteAsync(
                id, userId.Value, ct);

            return deleted
                ? NoContent()
                : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
