
using System.ComponentModel.DataAnnotations;

using FinanceManager.Api.Filters;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;
using FinanceManager.Infrastructure.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/account-settings")]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
public sealed class AccountSettingsController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserSessionService _sessions;
    private readonly FinanceDbContext _db;
    private readonly IEmailService _email;
    private readonly IConfiguration _configuration;

    public AccountSettingsController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager,
        UserSessionService sessions,
        FinanceDbContext db,
        IEmailService email,
        IConfiguration configuration)
    {
        _users = users;
        _signInManager = signInManager;
        _sessions = sessions;
        _db = db;
        _email = email;
        _configuration = configuration;
    }

    // GET /api/v1/account-settings/profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _users.GetUserAsync(User);

        if (user is null)
            return Unauthorized();

        return Ok(ToProfile(user));
    }

    // PUT /api/v1/account-settings/profile
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request)
    {
        var user = await _users.GetUserAsync(User);

        if (user is null)
            return Unauthorized();

        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();

        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName))
        {
            return BadRequest(new
            {
                message = "First and last name are required."
            });
        }

        if (firstName.Length > 100 || lastName.Length > 100)
        {
            return BadRequest(new
            {
                message = "Names cannot exceed 100 characters."
            });
        }

        user.FirstName = firstName;
        user.LastName = lastName;

        var result = await _users.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Unable to update your profile.",
                errors = result.Errors.Select(x => x.Description)
            });
        }

        return Ok(ToProfile(user));
    }

    // POST /api/v1/account-settings/change-password
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request)
    {
        var user = await _users.GetUserAsync(User);

        if (user is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new
            {
                message = "Both password fields are required."
            });
        }

        if (request.CurrentPassword == request.NewPassword)
        {
            return BadRequest(new
            {
                message =
                    "The new password must be different from your current password."
            });
        }

        var result = await _users.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "Password change failed. Check your current password and new password requirements.",
                errors = result.Errors.Select(x => x.Description)
            });
        }

        // Identity changes the security stamp during
        // a successful password change.
        //
        // Existing cookies are invalidated by the
        // SecurityStampValidator configuration already
        // established in Program.cs.
        //
        // Sign out this browser as well. The user will
        // need to sign in with the new password.
        await _sessions.RevokeAllAsync(
            user.Id,
            exceptSessionId: null,
            ct: HttpContext.RequestAborted);

        await _signInManager.SignOutAsync();

        return Ok(new
        {
            message =
                "Password changed successfully. Please sign in again."
        });
    }

    [HttpPost("change-email")]
    [EnableRateLimiting("auth-verification")]
    public async Task<IActionResult> RequestEmailChange(
        [FromBody] RequestEmailChangeRequest request,
        CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);

        if (user is null)
            return Unauthorized();

        var newEmail = request.NewEmail?.Trim();

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(newEmail) ||
            newEmail.Length > 254 ||
            !new EmailAddressAttribute().IsValid(newEmail))
        {
            return BadRequest(new
            {
                message = "Enter a valid new email and your current password."
            });
        }

        if (string.Equals(
                user.Email,
                newEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "The new email must be different from your current email."
            });
        }

        var existingUser = await _users.FindByEmailAsync(newEmail);

        if (existingUser is not null && existingUser.Id != user.Id)
            return Conflict(new { message = "That email address is already in use." });

        if (!await _users.CheckPasswordAsync(user, request.CurrentPassword))
            return BadRequest(new { message = "The current password is incorrect." });

        var token = await _users.GenerateChangeEmailTokenAsync(user, newEmail);
        var encodedToken = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));
        var frontendUrl = _configuration["Frontend:BaseUrl"];

        if (string.IsNullOrWhiteSpace(frontendUrl))
        {
            throw new InvalidOperationException(
                "Frontend:BaseUrl must be configured.");
        }

        var confirmationUrl = QueryHelpers.AddQueryString(
            $"{frontendUrl.TrimEnd('/')}/confirm-email-change",
            new Dictionary<string, string?>
            {
                ["email"] = user.Email,
                ["newEmail"] = newEmail,
                ["token"] = encodedToken
            });

        await _email.SendEmailChangeAsync(newEmail, confirmationUrl, ct);

        return Ok(new
        {
            message = "A confirmation link has been sent to your new email address."
        });
    }

    [AllowAnonymous]
    [HttpPost("confirm-email-change")]
    [EnableRateLimiting("auth-verification")]
    public async Task<IActionResult> ConfirmEmailChange(
        [FromBody] ConfirmEmailChangeRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.NewEmail) ||
            string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new
            {
                message = "The email confirmation link is invalid or expired."
            });
        }

        string token;

        try
        {
            token = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return BadRequest(new
            {
                message = "The email confirmation link is invalid or expired."
            });
        }

        var user = await _users.FindByEmailAsync(request.Email.Trim());

        if (user is null ||
            request.NewEmail.Length > 254 ||
            !new EmailAddressAttribute().IsValid(request.NewEmail))
        {
            return BadRequest(new
            {
                message = "The email confirmation link is invalid or expired."
            });
        }

        var existingUser = await _users.FindByEmailAsync(request.NewEmail);

        if (existingUser is not null && existingUser.Id != user.Id)
            return Conflict(new { message = "That email address is already in use." });

        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        var emailResult = await _users.ChangeEmailAsync(
            user,
            request.NewEmail,
            token);

        if (!emailResult.Succeeded)
        {
            await transaction.RollbackAsync(ct);

            return BadRequest(new
            {
                message = "The email confirmation link is invalid or expired."
            });
        }

        var usernameResult = await _users.SetUserNameAsync(
            user,
            request.NewEmail);

        if (!usernameResult.Succeeded)
        {
            await transaction.RollbackAsync(ct);

            return BadRequest(new
            {
                message = "Unable to update the account sign-in name.",
                errors = usernameResult.Errors.Select(x => x.Description)
            });
        }

        await _sessions.RevokeAllAsync(
            user.Id,
            exceptSessionId: null,
            ct);

        await transaction.CommitAsync(ct);

        return Ok(new
        {
            message = "Your email address has been updated. Please sign in again."
        });
    }

    private static AccountProfileResponse ToProfile(
        ApplicationUser user)
    {
        return new AccountProfileResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.EmailConfirmed,
            user.CreatedAtUtc);
    }
}

public sealed record UpdateProfileRequest(
    string? FirstName,
    string? LastName);

public sealed record ChangePasswordRequest(
    string? CurrentPassword,
    string? NewPassword);

public sealed record RequestEmailChangeRequest(
    string? NewEmail,
    string? CurrentPassword);

public sealed record ConfirmEmailChangeRequest(
    string Email,
    string NewEmail,
    string Token);

public sealed record AccountProfileResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool EmailConfirmed,
    DateTime CreatedAtUtc);
