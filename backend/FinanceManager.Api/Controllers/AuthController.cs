
using System.Security.Claims;

using FinanceManager.Application.DTOs.Auth;
using FinanceManager.Application.Services;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Api.Filters;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly DefaultCategorySeeder _defaultCategorySeeder;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        DefaultCategorySeeder defaultCategorySeeder)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _defaultCategorySeeder = defaultCategorySeeder;
    }

    // POST /api/v1/auth/register
    [HttpPost("register")]
    [AllowAnonymous]
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
            Email = email
        };

        // Create the user through ASP.NET Core Identity.
        var result = await _userManager.CreateAsync(
            user,
            request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                errors = result.Errors.Select(
                    x => x.Description)
            });
        }

        // Create default expense and income categories
        // for the newly registered user.
        await _defaultCategorySeeder.SeedForUserAsync(
            user.Id,
            HttpContext.RequestAborted);

        // Registration does not automatically authenticate.
        return StatusCode(
            StatusCodes.Status201Created,
            new AuthUserResponse(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email!
            ));
    }

    // POST /api/v1/auth/login
    [HttpPost("login")]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(
            request.Email?.Trim() ?? string.Empty);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            request.Password ?? string.Empty,
            isPersistent: false,
            lockoutOnFailure: true
        );

        if (!result.Succeeded)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        return Ok(new AuthUserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!
        ));
    }

    // GET /api/v1/auth/me
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
            return Unauthorized();

        return Ok(new AuthUserResponse(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email!
        ));
    }

    // POST /api/v1/auth/logout
    [HttpPost("logout")]
    [ServiceFilter(typeof(ValidateCsrfTokenFilter))]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return NoContent();
    }
}
