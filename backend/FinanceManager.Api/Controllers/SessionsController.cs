using FinanceManager.Api.Filters;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/sessions")]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
public sealed class SessionsController : ControllerBase
{
    private readonly UserSessionService _sessions;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public SessionsController(
        UserSessionService sessions,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager)
    {
        _sessions = sessions;
        _users = users;
        _signInManager = signInManager;
    }

    // GET /api/v1/sessions
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        var currentId = GetCurrentSessionId();

        var items = await _sessions.ListActiveAsync(userId.Value, ct);

        return Ok(items.Select(x => new UserSessionResponse(
            x.Id,
            x.DeviceName,
            x.CreatedAtUtc,
            x.LastSeenAtUtc,
            x.ExpiresAtUtc,
            x.Id == currentId)));
    }

    // DELETE /api/v1/sessions/{id}
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(
        Guid id,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        // Scoped to the authenticated user, so another
        // account's session id behaves like a missing one.
        var revoked = await _sessions.RevokeAsync(userId.Value, id, ct);

        if (!revoked)
            return NotFound();

        if (id == GetCurrentSessionId())
            await _signInManager.SignOutAsync();

        return NoContent();
    }

    // POST /api/v1/sessions/revoke-others
    [HttpPost("revoke-others")]
    public async Task<IActionResult> RevokeOthers(CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        var count = await _sessions.RevokeAllAsync(
            userId.Value,
            GetCurrentSessionId(),
            ct);

        return Ok(new { revoked = count });
    }

    // POST /api/v1/sessions/revoke-all
    [HttpPost("revoke-all")]
    public async Task<IActionResult> RevokeAll(CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        var count = await _sessions.RevokeAllAsync(
            userId.Value,
            exceptSessionId: null,
            ct);

        await _signInManager.SignOutAsync();

        return Ok(new { revoked = count });
    }

    private Guid? GetUserId()
    {
        var value = _users.GetUserId(User);

        return Guid.TryParse(value, out var id) ? id : null;
    }

    private Guid? GetCurrentSessionId()
    {
        var value = User
            .FindFirst(UserSessionService.SessionClaimType)?.Value;

        return Guid.TryParse(value, out var id) ? id : null;
    }
}

public sealed record UserSessionResponse(
    Guid Id,
    string DeviceName,
    DateTime CreatedAtUtc,
    DateTime LastSeenAtUtc,
    DateTime ExpiresAtUtc,
    bool IsCurrent);
