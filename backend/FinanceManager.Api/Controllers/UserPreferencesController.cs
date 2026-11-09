using FinanceManager.Api.Filters;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/user-preferences")]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
public sealed class UserPreferencesController : ControllerBase
{
    private readonly FinanceDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    private static readonly HashSet<string> AllowedCurrencies =
        new(StringComparer.Ordinal)
        {
            "CAD",
            "USD"
        };

    private static readonly HashSet<string> AllowedDateFormats =
        new(StringComparer.Ordinal)
        {
            "yyyy-MM-dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy"
        };

    public UserPreferencesController(
        FinanceDbContext db,
        UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        var preference = await _db.UserPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId.Value,
                ct);

        if (preference is null)
        {
            preference = new UserPreference
            {
                UserId = userId.Value
            };
        }

        return Ok(ToResponse(preference));
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromBody] UpdateUserPreferencesRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();

        if (userId is null)
            return Unauthorized();

        var currency = request.DefaultCurrency?
            .Trim()
            .ToUpperInvariant();

        var timeZone = request.TimeZone?.Trim();
        var dateFormat = request.DateFormat?.Trim();

        if (currency is null ||
            !AllowedCurrencies.Contains(currency))
        {
            return BadRequest(new
            {
                message = "Choose a supported currency: CAD or USD."
            });
        }

        if (dateFormat is null ||
            !AllowedDateFormats.Contains(dateFormat))
        {
            return BadRequest(new
            {
                message = "Choose a supported date format."
            });
        }

        if (string.IsNullOrWhiteSpace(timeZone) ||
            timeZone.Length > 100 ||
            !IsValidTimeZone(timeZone))
        {
            return BadRequest(new
            {
                message = "Choose a valid timezone."
            });
        }

        if (request.BudgetStartDay < 1 ||
            request.BudgetStartDay > 28)
        {
            return BadRequest(new
            {
                message =
                    "Budget start day must be between 1 and 28."
            });
        }

        var updatedAt = DateTime.UtcNow;

        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserPreferences"
            (
                "UserId",
                "DefaultCurrency",
                "TimeZone",
                "DateFormat",
                "BudgetStartDay",
                "UpdatedAtUtc"
            )
            VALUES
            (
                {userId.Value},
                {currency},
                {timeZone},
                {dateFormat},
                {request.BudgetStartDay},
                {updatedAt}
            )
            ON CONFLICT ("UserId")
            DO UPDATE SET
                "DefaultCurrency" =
                    EXCLUDED."DefaultCurrency",
                "TimeZone" =
                    EXCLUDED."TimeZone",
                "DateFormat" =
                    EXCLUDED."DateFormat",
                "BudgetStartDay" =
                    EXCLUDED."BudgetStartDay",
                "UpdatedAtUtc" =
                    EXCLUDED."UpdatedAtUtc";
            """, ct);

        return Ok(new UserPreferencesResponse(
            currency,
            timeZone,
            dateFormat,
            request.BudgetStartDay,
            updatedAt));
    }

    private Guid? GetUserId()
    {
        var value = _users.GetUserId(User);

        return Guid.TryParse(value, out var id)
            ? id
            : null;
    }

    private static bool IsValidTimeZone(string timeZone)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static UserPreferencesResponse ToResponse(
        UserPreference preference)
    {
        return new UserPreferencesResponse(
            preference.DefaultCurrency,
            preference.TimeZone,
            preference.DateFormat,
            preference.BudgetStartDay,
            preference.UpdatedAtUtc);
    }
}

public sealed record UpdateUserPreferencesRequest(
    string? DefaultCurrency,
    string? TimeZone,
    string? DateFormat,
    int BudgetStartDay);

public sealed record UserPreferencesResponse(
    string DefaultCurrency,
    string TimeZone,
    string DateFormat,
    int BudgetStartDay,
    DateTime UpdatedAtUtc);
