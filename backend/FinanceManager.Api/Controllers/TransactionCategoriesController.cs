
using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.Categories;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/categories")]
public class TransactionCategoriesController : ControllerBase
{
    private readonly ITransactionCategoryService _service;
    private readonly UserManager<ApplicationUser> _userManager;

    public TransactionCategoriesController(
        ITransactionCategoryService service,
        UserManager<ApplicationUser> userManager)
    {
        _service = service;
        _userManager = userManager;
    }

    private Guid? GetCurrentUserId()
    {
        var value = _userManager.GetUserId(User);

        return Guid.TryParse(value, out var id)
            ? id
            : null;
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
            userId.Value, ct);

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
            id, userId.Value, ct);

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
                userId.Value, request, ct);

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
                id, userId.Value, request, ct);

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
                id, userId.Value, ct);

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
