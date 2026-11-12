using System.Globalization;

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
[Route("api/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly UserManager<ApplicationUser> _users;

    public ReportsController(IReportService reports, UserManager<ApplicationUser> users)
    {
        _reports = reports;
        _users = users;
    }

    // GET /api/v1/reports/categories?from=2026-01&to=2026-06&currency=CAD
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategoryReport(
        [FromQuery] string from,
        [FromQuery] string to,
        [FromQuery] string? currency,
        CancellationToken ct)
    {
        if (!Guid.TryParse(_users.GetUserId(User), out var userId))
            return Unauthorized();

        if (!TryParseMonth(from, out var fromMonth) || !TryParseMonth(to, out var toMonth))
            return BadRequest(new { message = "Use months in the form yyyy-MM." });

        try
        {
            return Ok(await _reports.GetCategoryReportAsync(userId, fromMonth, toMonth, currency, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private static bool TryParseMonth(string? value, out DateOnly month) =>
        DateOnly.TryParseExact(
            value + "-01",
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out month);
}
