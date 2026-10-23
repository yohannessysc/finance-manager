
using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.Categories;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/categories")]
public class TransactionCategoriesController : ControllerBase
{
    private readonly ITransactionCategoryService _service;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FinanceDbContext _db;

    public TransactionCategoriesController(
        ITransactionCategoryService service,
        UserManager<ApplicationUser> userManager,
        FinanceDbContext db)
    {
        _service = service;
        _userManager = userManager;
        _db = db;
    }

    private Guid? GetCurrentUserId()
    {
        var value = _userManager.GetUserId(User);

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    // GET /api/v1/categories/types
    [HttpGet("types")]
    public IActionResult GetTypes()
    {
        return Ok(new[]
        {
            new
            {
                name = nameof(TransactionType.Expense),
                value = (int)TransactionType.Expense
            },
            new
            {
                name = nameof(TransactionType.Income),
                value = (int)TransactionType.Income
            }
        });
    }

    // GET /api/v1/categories/usage
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var id = userId.Value;

        // Only count transactions belonging to this user.
        var transactionCounts = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(x => x.UserId == id && x.CategoryId != null)
            .GroupBy(x => x.CategoryId)
            .Select(group => new
            {
                CategoryId = group.Key,
                Count = group.Count()
            })
            .ToListAsync(ct);

        var recurringCounts = await _db.RecurringTransactions
            .AsNoTracking()
            .Where(x => x.UserId == id && x.CategoryId != null)
            .GroupBy(x => x.CategoryId)
            .Select(group => new
            {
                CategoryId = group.Key,
                Count = group.Count()
            })
            .ToListAsync(ct);

        // Make sure returned IDs are owned by this user.
        var ownedCategories = await _db.TransactionCategories
            .AsNoTracking()
            .Where(x => x.UserId == id)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var results = ownedCategories.Select(categoryId =>
        {
            var transactionCount = transactionCounts
                .FirstOrDefault(x => x.CategoryId == categoryId)
                ?.Count ?? 0;

            var recurringCount = recurringCounts
                .FirstOrDefault(x => x.CategoryId == categoryId)
                ?.Count ?? 0;

            return new
            {
                categoryId,
                transactionCount,
                recurringCount,
                inUse = transactionCount > 0 ||
                        recurringCount > 0
            };
        });

        return Ok(results);
    }

    // GET /api/v1/categories
    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var categories = await _service.GetAllAsync(
            userId.Value,
            ct);

        return Ok(categories);
    }

    // GET /api/v1/categories/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        var category = await _service.GetByIdAsync(
            id,
            userId.Value,
            ct);

        return category is null
            ? NotFound()
            : Ok(category);
    }

    // POST /api/v1/categories
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var category = await _service.CreateAsync(
                userId.Value,
                request,
                ct);

            return CreatedAtAction(
                nameof(GetById),
                new { id = category.Id },
                category);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT /api/v1/categories/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var category = await _service.UpdateAsync(
                id,
                userId.Value,
                request,
                ct);

            return category is null
                ? NotFound()
                : Ok(category);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // DELETE /api/v1/categories/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
            return Unauthorized();

        try
        {
            var deleted = await _service.DeleteAsync(
                id,
                userId.Value,
                ct);

            return deleted
                ? NoContent()
                : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
