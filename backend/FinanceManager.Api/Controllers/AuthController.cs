
using System.Text;

using FinanceManager.Api.Filters;
using FinanceManager.Application.DTOs.Auth;
using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private static readonly TimeSpan EmailCooldown =
        TimeSpan.FromSeconds(60);

    private const string EmailConfirmationPurpose =
        "EmailConfirmation";

    private const string PasswordResetPurpose =
        "PasswordReset";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly DefaultCategorySeeder _defaultCategorySeeder;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly FinanceDbContext _db;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        DefaultCategorySeeder defaultCategorySeeder,
        IEmailService emailService,
        IConfiguration configuration,
        FinanceDbContext db)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _defaultCategorySeeder = defaultCategorySeeder;
        _emailService = emailService;
        _configuration = configuration;
        _db = db;
    }

    // ---------------------------------------
    // REGISTER
    // ---------------------------------------

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-register")]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new
            {
                message = "All fields are required."
            });
        }

        var email = request.Email.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            UserName = email,
            Email = email,
            EmailConfirmed = false
        };

        var result = await _userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(
                    error => error.Description)
            });
        }

        await _defaultCategorySeeder.SeedForUserAsync(
            user.Id,
            HttpContext.RequestAborted);

        // Initial registration now uses the same cooldown
        // as the Resend Confirmation endpoint.
        await TrySendConfirmationEmailAsync(
            user,
            HttpContext.RequestAborted);

        return StatusCode(
            StatusCodes.Status201Created,
            new
            {
                message =
                    "Account created. Please verify your email before signing in."
            });
    }

    // ---------------------------------------
    // CONFIRM EMAIL
    // ---------------------------------------

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-verification")]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    public async Task<IActionResult> ConfirmEmail(
        [FromBody] ConfirmEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new
            {
                message = "Email and verification token are required."
            });
        }

        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null)
        {
            return BadRequest(new
            {
                message = "Invalid or expired verification link."
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
                message = "Invalid or expired verification link."
            });
        }

        var result = await _userManager.ConfirmEmailAsync(
            user,
            token);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message = "Invalid or expired verification link."
            });
        }

        return Ok(new
        {
            message = "Email verified successfully."
        });
    }

    // ---------------------------------------
    // LOGIN
    // ---------------------------------------

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        const string invalidCredentials =
            "Invalid email or password. Please check your credentials and try again.";

        var user = await _userManager.FindByEmailAsync(
            request.Email?.Trim() ?? string.Empty);

        if (user is null)
        {
            return Unauthorized(new
            {
                code = "INVALID_CREDENTIALS",
                message = invalidCredentials
            });
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            request.Password ?? string.Empty,
            isPersistent: false,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            return StatusCode(
                StatusCodes.Status423Locked,
                new
                {
                    code = "ACCOUNT_LOCKED",
                    message =
                        "Too many unsuccessful login attempts. Please try again later."
                });
        }

        if (result.IsNotAllowed)
        {
            // Only reveal verification status after the
            // supplied password has been checked.
            var passwordValid =
                await _userManager.CheckPasswordAsync(
                    user,
                    request.Password ?? string.Empty);

            if (passwordValid &&
                !await _userManager.IsEmailConfirmedAsync(user))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        code = "EMAIL_NOT_VERIFIED",
                        message =
                            "Please verify your email address before signing in."
                    });
            }
        }

        if (!result.Succeeded)
        {
            return Unauthorized(new
            {
                code = "INVALID_CREDENTIALS",
                message = invalidCredentials
            });
        }

        return Ok(new AuthUserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!));
    }

    // ---------------------------------------
    // CURRENT USER
    // ---------------------------------------

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new AuthUserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!));
    }

    // ---------------------------------------
    // LOGOUT
    // ---------------------------------------

    [HttpPost("logout")]
    [Authorize]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return NoContent();
    }

    // ---------------------------------------
    // RESEND EMAIL CONFIRMATION
    // ---------------------------------------

    [HttpPost("resend-confirmation")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-verification")]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    public async Task<IActionResult> ResendConfirmation(
        [FromBody] ResendConfirmationRequest request,
        CancellationToken ct)
    {
        const string successMessage =
            "If this account needs verification, " +
            "a verification email will be sent.";

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "An email address is required."
            });
        }

        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is not null &&
            !await _userManager.IsEmailConfirmedAsync(user))
        {
            await TrySendConfirmationEmailAsync(user, ct);
        }

        return Ok(new
        {
            message = successMessage
        });
    }

    // ---------------------------------------
    // FORGOT PASSWORD
    // ---------------------------------------

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-recovery")]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken ct)
    {
        const string successMessage =
            "If an eligible account exists for that email, " +
            "a password reset link will be sent.";

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "An email address is required."
            });
        }

        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is not null &&
            await _userManager.IsEmailConfirmedAsync(user))
        {
            await TrySendPasswordResetEmailAsync(user, ct);
        }

        return Ok(new
        {
            message = successMessage
        });
    }

    // ---------------------------------------
    // RESET PASSWORD
    // ---------------------------------------

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-recovery")]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request)
    {
        const string invalidLink =
            "Invalid or expired password reset link.";

        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Token) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new
            {
                message = "All password reset fields are required."
            });
        }

        var user = await _userManager.FindByEmailAsync(
            request.Email.Trim());

        if (user is null)
        {
            return BadRequest(new
            {
                message = invalidLink
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
                message = invalidLink
            });
        }

        var result = await _userManager.ResetPasswordAsync(
            user,
            token,
            request.NewPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                message =
                    "The reset link may have expired, or the new password does not meet security requirements.",
                errors = result.Errors.Select(
                    error => error.Description)
            });
        }

        return Ok(new
        {
            message = "Password reset successfully."
        });
    }

    // ---------------------------------------
    // SEND CONFIRMATION WITH COOLDOWN
    // ---------------------------------------

    private async Task TrySendConfirmationEmailAsync(
        ApplicationUser user,
        CancellationToken ct)
    {
        var reservation = await TryAcquireEmailCooldownAsync(
            user.Id,
            EmailConfirmationPurpose,
            ct);

        if (reservation is null)
        {
            return;
        }

        try
        {
            await SendConfirmationEmailAsync(user, ct);
        }
        catch
        {
            await ReleaseEmailCooldownAsync(
                user.Id,
                EmailConfirmationPurpose,
                reservation.Value,
                CancellationToken.None);

            throw;
        }
    }

    // ---------------------------------------
    // SEND RESET EMAIL WITH COOLDOWN
    // ---------------------------------------

    private async Task TrySendPasswordResetEmailAsync(
        ApplicationUser user,
        CancellationToken ct)
    {
        var reservation = await TryAcquireEmailCooldownAsync(
            user.Id,
            PasswordResetPurpose,
            ct);

        if (reservation is null)
        {
            return;
        }

        try
        {
            await SendPasswordResetEmailAsync(user, ct);
        }
        catch
        {
            await ReleaseEmailCooldownAsync(
                user.Id,
                PasswordResetPurpose,
                reservation.Value,
                CancellationToken.None);

            throw;
        }
    }

    // ---------------------------------------
    // ATOMIC COOLDOWN RESERVATION
    // ---------------------------------------

    private async Task<DateTime?> TryAcquireEmailCooldownAsync(
        Guid userId,
        string purpose,
        CancellationToken ct)
    {
        // PostgreSQL stores microsecond precision.
        // Normalize the timestamp before inserting so
        // equality checks during release are reliable.
        var utcNow = DateTime.UtcNow;

        var reservationTime = new DateTime(
            utcNow.Ticks - (utcNow.Ticks % 10),
            DateTimeKind.Utc);

        var cutoff = reservationTime - EmailCooldown;

        var affectedRows = await _db.Database
            .ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "EmailSendCooldowns"
                    ("UserId", "Purpose", "LastSentAtUtc")
                VALUES
                    ({userId}, {purpose}, {reservationTime})
                ON CONFLICT ("UserId", "Purpose")
                DO UPDATE SET
                    "LastSentAtUtc" = EXCLUDED."LastSentAtUtc"
                WHERE "EmailSendCooldowns"."LastSentAtUtc"
                    <= {cutoff};
                """, ct);

        return affectedRows == 1
            ? reservationTime
            : null;
    }

    // ---------------------------------------
    // SAFE COOLDOWN RELEASE
    // ---------------------------------------

    private async Task ReleaseEmailCooldownAsync(
        Guid userId,
        string purpose,
        DateTime reservationTime,
        CancellationToken ct)
    {
        // Only release our own reservation.
        // If another request has replaced the timestamp,
        // this DELETE will not affect it.
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "EmailSendCooldowns"
            WHERE "UserId" = {userId}
              AND "Purpose" = {purpose}
              AND "LastSentAtUtc" = {reservationTime};
            """, ct);
    }

    // ---------------------------------------
    // EMAIL CONFIRMATION DELIVERY
    // ---------------------------------------

    private async Task SendConfirmationEmailAsync(
        ApplicationUser user,
        CancellationToken ct)
    {
        var token = await _userManager
            .GenerateEmailConfirmationTokenAsync(user);

        var encodedToken = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));

        var confirmationUrl =
            $"{GetFrontendUrl()}/confirm-email" +
            $"?email={Uri.EscapeDataString(user.Email!)}" +
            $"&token={Uri.EscapeDataString(encodedToken)}";

        await _emailService.SendEmailConfirmationAsync(
            user.Email!,
            confirmationUrl,
            ct);
    }

    // ---------------------------------------
    // PASSWORD RESET EMAIL DELIVERY
    // ---------------------------------------

    private async Task SendPasswordResetEmailAsync(
        ApplicationUser user,
        CancellationToken ct)
    {
        var token = await _userManager
            .GeneratePasswordResetTokenAsync(user);

        var encodedToken = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));

        var resetUrl =
            $"{GetFrontendUrl()}/reset-password" +
            $"?email={Uri.EscapeDataString(user.Email!)}" +
            $"&token={Uri.EscapeDataString(encodedToken)}";

        await _emailService.SendPasswordResetAsync(
            user.Email!,
            resetUrl,
            ct);
    }

    private string GetFrontendUrl()
    {
        var frontendUrl = _configuration["Frontend:BaseUrl"];

        if (string.IsNullOrWhiteSpace(frontendUrl))
        {
            throw new InvalidOperationException(
                "Frontend:BaseUrl must be configured.");
        }

        return frontendUrl.TrimEnd('/');
    }
}

public record ConfirmEmailRequest(
    string Email,
    string Token);

public record ResendConfirmationRequest(
    string Email);

public record ForgotPasswordRequest(
    string Email);

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword);
