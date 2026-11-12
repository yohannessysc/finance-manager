using System.Text.Json;

using FinanceManager.Api.Filters;
using FinanceManager.Application.Interfaces;
using FinanceManager.Infrastructure.Identity;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/account-privacy")]
[ServiceFilter(typeof(ValidateCsrfTokenFilter))]
public sealed class AccountPrivacyController : ControllerBase
{
    private readonly FinanceDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IBillingService _billing;
    private readonly IBankConnectionService _bankConnections;
    private readonly ILogger<AccountPrivacyController> _logger;

    public AccountPrivacyController(
        FinanceDbContext db,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signInManager,
        IBillingService billing,
        IBankConnectionService bankConnections,
        ILogger<AccountPrivacyController> logger)
    {
        _db = db;
        _users = users;
        _signInManager = signInManager;
        _billing = billing;
        _bankConnections = bankConnections;
        _logger = logger;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);

        if (user is null)
            return Unauthorized();

        var userId = user.Id;

        var preferences = await _db.UserPreferences
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);

        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var categories = await _db.TransactionCategories
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var transactions = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.TransactionDateUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var transactionIds = transactions.Select(x => x.Id).ToList();
        var splits = (await _db.TransactionSplits
                .AsNoTracking()
                .Where(x => transactionIds.Contains(x.TransactionId))
                .ToListAsync(ct))
            .ToLookup(x => x.TransactionId);

        // Receipt files are listed (not embedded); each can be downloaded
        // from its transaction in the app.
        var attachments = (await _db.TransactionAttachments
                .AsNoTracking()
                .Where(x => transactionIds.Contains(x.TransactionId))
                .Select(x => new { x.Id, x.TransactionId, x.FileName, x.ContentType, x.SizeBytes, x.CreatedAtUtc })
                .ToListAsync(ct))
            .ToLookup(x => x.TransactionId);

        var recurringTransactions = await _db.RecurringTransactions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.NextOccurrenceUtc)
            .ToListAsync(ct);

        var budgets = await _db.MonthlyBudgets
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CategoryId)
            .ThenBy(x => x.Currency)
            .ToListAsync(ct);

        var savingsGoals = await _db.SavingsGoals
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var goalIds = savingsGoals.Select(x => x.Id).ToArray();
        var goalContributions = await _db.SavingsGoalContributions
            .AsNoTracking()
            .Where(x => x.UserId == userId && goalIds.Contains(x.SavingsGoalId))
            .OrderBy(x => x.ContributionDateUtc)
            .ToListAsync(ct);

        var export = new
        {
            schemaVersion = 1,
            exportedAtUtc = DateTime.UtcNow,
            profile = new
            {
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email,
                user.EmailConfirmed,
                user.CreatedAtUtc
            },
            preferences = preferences is null
                ? null
                : new
                {
                    preferences.DefaultCurrency,
                    preferences.TimeZone,
                    preferences.DateFormat,
                    preferences.BudgetStartDay,
                    preferences.UpdatedAtUtc
                },
            accounts = accounts.Select(x => new
            {
                x.Id,
                x.Name,
                x.AccountType,
                x.Currency,
                x.OpeningBalance,
                x.CreatedAtUtc
            }),
            categories = categories.Select(x => new
            {
                x.Id,
                x.Name,
                x.Type,
                x.ParentCategoryId,
                x.CreatedAtUtc
            }),
            transactions = transactions.Select(x => new
            {
                x.Id,
                x.FinancialAccountId,
                x.DestinationAccountId,
                x.CategoryId,
                x.RecurringTransactionId,
                x.ScheduledOccurrenceUtc,
                x.Type,
                x.Amount,
                x.Description,
                x.Merchant,
                x.TransactionDateUtc,
                x.CreatedAtUtc,
                splits = splits[x.Id].Select(s => new { s.CategoryId, s.Amount, s.Note }),
                receipts = attachments[x.Id].Select(a => new { a.Id, a.FileName, a.ContentType, a.SizeBytes, a.CreatedAtUtc })
            }),
            recurringTransactions = recurringTransactions.Select(x => new
            {
                x.Id,
                x.FinancialAccountId,
                x.DestinationAccountId,
                x.CategoryId,
                x.Type,
                x.Amount,
                x.Description,
                x.Frequency,
                x.Status,
                x.TimeZoneId,
                x.StartDateUtc,
                x.NextOccurrenceUtc,
                x.EndDateUtc,
                x.CreatedAtUtc
            }),
            budgets = budgets.Select(x => new
            {
                x.Id,
                x.CategoryId,
                x.Currency,
                x.MonthlyLimit,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            }),
            savingsGoals = savingsGoals.Select(x => new
            {
                x.Id,
                x.Name,
                x.Currency,
                x.TargetAmount,
                x.TargetDateUtc,
                x.CreatedAtUtc,
                x.UpdatedAtUtc
            }),
            savingsGoalContributions = goalContributions.Select(x => new
            {
                x.Id,
                x.SavingsGoalId,
                x.Amount,
                x.ContributionDateUtc,
                x.Note,
                x.CreatedAtUtc
            })
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            export,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true
            });

        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(
            bytes,
            "application/json; charset=utf-8",
            $"finance-manager-export-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(
        [FromBody] DeleteAccountRequest request,
        CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);

        if (user is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            request.Confirmation != "DELETE")
        {
            return BadRequest(new
            {
                message =
                    "Enter your current password and type DELETE to confirm."
            });
        }

        if (!await _users.CheckPasswordAsync(user, request.CurrentPassword))
            return BadRequest(new { message = "The current password is incorrect." });

        // Stop external billing before deleting anything: a deleted
        // account must never keep being charged.
        try
        {
            await _billing.CancelSubscriptionAsync(user.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Subscription cancellation failed for user {UserId}.", user.Id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "We could not cancel your subscription, so your account was not deleted. Please try again or contact support."
            });
        }

        // Revoke bank access at Plaid so linked items stop being billed.
        foreach (var connection in await _bankConnections.GetUserConnectionsAsync(user.Id))
            await _bankConnections.DisconnectAsync(connection.Id, user.Id);

        await using var transaction =
            await _db.Database.BeginTransactionAsync(ct);

        await _db.FinancialTransactions
            .Where(x => x.UserId == user.Id)
            .ExecuteDeleteAsync(ct);

        await _db.RecurringTransactions
            .Where(x => x.UserId == user.Id)
            .ExecuteDeleteAsync(ct);

        await _db.MonthlyBudgets
            .Where(x => x.UserId == user.Id)
            .ExecuteDeleteAsync(ct);

        await _db.FinancialAccounts
            .Where(x => x.UserId == user.Id)
            .ExecuteDeleteAsync(ct);

        // Detach subcategories first: the parent link is RESTRICT, which
        // Postgres checks row by row within a single DELETE.
        await _db.TransactionCategories
            .Where(x => x.UserId == user.Id && x.ParentCategoryId != null)
            .ExecuteUpdateAsync(x => x.SetProperty(c => c.ParentCategoryId, (Guid?)null), ct);

        await _db.TransactionCategories
            .Where(x => x.UserId == user.Id)
            .ExecuteDeleteAsync(ct);

        var result = await _users.DeleteAsync(user);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(ct);

            return Conflict(new
            {
                message = "The account could not be deleted.",
                errors = result.Errors.Select(x => x.Description)
            });
        }

        await transaction.CommitAsync(ct);
        await _signInManager.SignOutAsync();

        return NoContent();
    }
}

public sealed record DeleteAccountRequest(
    string? CurrentPassword,
    string? Confirmation);
