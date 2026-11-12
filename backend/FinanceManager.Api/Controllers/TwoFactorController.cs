using System.Text;

using FinanceManager.Api.Filters;
using FinanceManager.Infrastructure.Identity;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

// Authenticator-app (TOTP) two-factor authentication for the signed-in user.
[ApiController]
[Authorize]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
[Route("api/v1/account/two-factor")]
public sealed class TwoFactorController : ControllerBase
{
    private const string Issuer = "Finance Manager";
    private const int RecoveryCodeCount = 10;

    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;

    public TwoFactorController(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn)
    {
        _users = users;
        _signIn = signIn;
    }

    [HttpGet]
    public async Task<IActionResult> GetStatus()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        return Ok(new
        {
            enabled = await _users.GetTwoFactorEnabledAsync(user),
            recoveryCodesLeft = await _users.CountRecoveryCodesAsync(user)
        });
    }

    // Starts setup with a fresh secret; nothing changes until Enable
    // verifies a code from the user's app.
    [HttpPost("setup")]
    public async Task<IActionResult> Setup()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        if (await _users.GetTwoFactorEnabledAsync(user))
            return Conflict(new { message = "Two-factor authentication is already on." });

        await _users.ResetAuthenticatorKeyAsync(user);
        // Resetting the key rotates the security stamp; re-issue this
        // device's cookie (keeping its session) so it stays signed in.
        await _signIn.RefreshSignInKeepingSessionAsync(user, User);

        var key = await _users.GetAuthenticatorKeyAsync(user) ?? string.Empty;
        var label = Uri.EscapeDataString($"{Issuer}:{user.Email}");

        return Ok(new
        {
            sharedKey = FormatKey(key),
            authenticatorUri =
                $"otpauth://totp/{label}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6"
        });
    }

    [HttpPost("enable")]
    public async Task<IActionResult> Enable([FromBody] TwoFactorCodeRequest request)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        var valid = await _users.VerifyTwoFactorTokenAsync(
            user,
            _users.Options.Tokens.AuthenticatorTokenProvider,
            (request.Code ?? string.Empty).Replace(" ", ""));

        if (!valid)
            return BadRequest(new { message = "That code is not valid. Check your authenticator app and try again." });

        await _users.SetTwoFactorEnabledAsync(user, true);
        var codes = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        await _signIn.RefreshSignInKeepingSessionAsync(user, User);

        return Ok(new { recoveryCodes = codes });
    }

    [HttpPost("disable")]
    public async Task<IActionResult> Disable([FromBody] TwoFactorPasswordRequest request)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        if (!await _users.CheckPasswordAsync(user, request.Password ?? string.Empty))
            return BadRequest(new { message = "The current password is incorrect." });

        await _users.SetTwoFactorEnabledAsync(user, false);
        await _users.ResetAuthenticatorKeyAsync(user);
        await _signIn.RefreshSignInKeepingSessionAsync(user, User);

        return NoContent();
    }

    [HttpPost("recovery-codes")]
    public async Task<IActionResult> RegenerateRecoveryCodes([FromBody] TwoFactorPasswordRequest request)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null)
            return Unauthorized();

        if (!await _users.GetTwoFactorEnabledAsync(user))
            return Conflict(new { message = "Turn on two-factor authentication first." });

        if (!await _users.CheckPasswordAsync(user, request.Password ?? string.Empty))
            return BadRequest(new { message = "The current password is incorrect." });

        var codes = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
        return Ok(new { recoveryCodes = codes });
    }

    // Groups of four, as authenticator apps display them.
    private static string FormatKey(string key)
    {
        var formatted = new StringBuilder();
        for (var i = 0; i < key.Length; i += 4)
        {
            if (i > 0)
                formatted.Append(' ');
            formatted.Append(key.AsSpan(i, Math.Min(4, key.Length - i)));
        }
        return formatted.ToString().ToLowerInvariant();
    }
}

public sealed record TwoFactorCodeRequest(string Code);

public sealed record TwoFactorPasswordRequest(string Password);
