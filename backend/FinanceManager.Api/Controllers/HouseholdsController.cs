using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.Households;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/households")]
public sealed class HouseholdsController : ControllerBase
{
    private readonly IHouseholdService _households;
    private readonly UserManager<ApplicationUser> _users;

    public HouseholdsController(
        IHouseholdService households,
        UserManager<ApplicationUser> users)
    {
        _households = households;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        return userId is null
            ? Unauthorized()
            : Ok(await _households.GetAllAsync(userId.Value, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateHouseholdRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            return Ok(await _households.CreateAsync(
                userId.Value,
                request,
                ct));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{householdId:guid}/invitations")]
    public async Task<IActionResult> Invite(
        Guid householdId,
        [FromBody] InviteHouseholdMemberRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var invitation = await _households.InviteAsync(
                userId.Value,
                householdId,
                request,
                ct);
            return Ok(invitation);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (HttpRequestException)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { message = "Invitation email could not be delivered." });
        }
    }

    [HttpPost("invitations/accept")]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptHouseholdInvitationRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var accepted = await _households.AcceptInvitationAsync(
            userId.Value,
            request,
            ct);
        return accepted
            ? NoContent()
            : BadRequest(new
            {
                message = "Invitation is invalid, expired, or not addressed to this verified account."
            });
    }

    [HttpDelete("{householdId:guid}/members/{memberUserId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid householdId,
        Guid memberUserId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            return await _households.RemoveMemberAsync(
                userId.Value,
                householdId,
                memberUserId,
                ct)
                ? NoContent()
                : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("{householdId:guid}/members/{memberUserId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid householdId,
        Guid memberUserId,
        [FromBody] UpdateHouseholdMemberRoleRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            return await _households.UpdateMemberRoleAsync(
                userId.Value,
                householdId,
                memberUserId,
                request,
                ct)
                ? NoContent()
                : NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var userId = _users.GetUserId(User);
        return Guid.TryParse(userId, out var id) ? id : null;
    }
}
