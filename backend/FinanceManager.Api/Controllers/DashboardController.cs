
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
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(
        IDashboardService dashboardService,
        UserManager<ApplicationUser> userManager)
    {
        _dashboardService = dashboardService;
        _userManager = userManager;
    }

    private Guid? GetCurrentUserId()
    {
        var value = _userManager.GetUserId(User);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized();
        }

        var summary = await _dashboardService.GetSummaryAsync(
            userId.Value,
            ct);

        return Ok(summary);
    }
}
