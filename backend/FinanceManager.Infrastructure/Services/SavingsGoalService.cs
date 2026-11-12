using FinanceManager.Application.DTOs.SavingsGoals;
using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public sealed class SavingsGoalService : ISavingsGoalService
{
    private const decimal MaximumAmount = 9999999999999999.99m;
    private const int MaximumEmergencyFundMonths = 24;

    private readonly FinanceDbContext _db;
    private readonly IHouseholdAuthorizationService _householdAuthorization;
    private readonly SavingsGoalProgressReader _progress;

    public SavingsGoalService(
        FinanceDbContext db,
        IHouseholdAuthorizationService householdAuthorization)
    {
        _db = db;
        _householdAuthorization = householdAuthorization;
        _progress = new SavingsGoalProgressReader(db);
    }

    public async Task<List<SavingsGoalResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var householdIds = await _householdAuthorization
            .GetHouseholdIdsAsync(userId, ct);

        var goals = await _db.SavingsGoals
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId ||
                (x.HouseholdId.HasValue &&
                 householdIds.Contains(x.HouseholdId.Value)))
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var progress = await _progress.ReadAsync(goals, DateTime.UtcNow, ct);
        var results = new List<SavingsGoalResponse>(goals.Count);

        foreach (var goal in goals)
        {
            results.Add(ToResponse(
                goal,
                progress[goal.Id],
                await CanEditAsync(goal, userId, ct)));
        }

        return results;
    }

    public async Task<SavingsGoalResponse> CreateAsync(
        Guid userId,
        SavingsGoalUpsertRequest request,
        CancellationToken ct = default)
    {
        ValidateName(request.Name);
        ValidateAmount(request.TargetAmount, "Target amount");
        ValidateTargetDate(request.TargetDateUtc);
        ValidateGoalType(request.GoalType, request.EmergencyFundMonths);

        var currency = NormalizeCurrency(request.Currency);
        await EnsureUserHasCurrencyAccountAsync(userId, currency, ct);

        if (request.HouseholdId is { } householdId &&
            !await _householdAuthorization.CanEditAsync(userId, householdId, ct))
        {
            throw new UnauthorizedAccessException(
                "Your household role does not allow shared savings goal changes.");
        }

        var now = DateTime.UtcNow;
        var goal = new SavingsGoal
        {
            UserId = userId,
            HouseholdId = request.HouseholdId,
            Name = request.Name.Trim(),
            Currency = currency,
            GoalType = request.GoalType,
            EmergencyFundMonths = request.EmergencyFundMonths,
            TargetAmount = request.TargetAmount,
            TargetDateUtc = request.TargetDateUtc,
            NotificationsEnabled = request.NotificationsEnabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await ApplyTrackingAsync(
            goal,
            request.LinkedAccountId,
            request.AutoContributionAmount,
            request.AutoContributionFrequency,
            request.AutoContributionStartUtc,
            ct);

        var progress = await ReadProgressAsync(goal, ct);

        // Only milestones crossed from here on are worth announcing;
        // a goal linked to a well-funded account starts part way.
        goal.LastMilestoneNotified = SavingsGoalProjectionCalculator
            .MilestoneFor(progress.AmountSaved, progress.TargetAmount);

        _db.SavingsGoals.Add(goal);
        await _db.SaveChangesAsync(ct);

        return ToResponse(goal, progress, canEdit: true);
    }

    public async Task<SavingsGoalResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        SavingsGoalUpdateRequest request,
        CancellationToken ct = default)
    {
        var goal = await FindEditableAsync(id, userId, ct);

        if (goal is null)
            return null;

        ValidateName(request.Name);
        ValidateAmount(request.TargetAmount, "Target amount");
        ValidateTargetDate(request.TargetDateUtc);
        ValidateGoalType(goal.GoalType, request.EmergencyFundMonths);

        goal.Name = request.Name.Trim();
        goal.TargetAmount = request.TargetAmount;
        goal.TargetDateUtc = request.TargetDateUtc;
        goal.EmergencyFundMonths = request.EmergencyFundMonths;
        goal.NotificationsEnabled = request.NotificationsEnabled;

        await ApplyTrackingAsync(
            goal,
            request.LinkedAccountId,
            request.AutoContributionAmount,
            request.AutoContributionFrequency,
            request.AutoContributionStartUtc,
            ct);

        var progress = await ReadProgressAsync(goal, ct);

        // If a bigger target pulled progress back, milestones can be
        // announced again when they are re-crossed. Pending ones are kept.
        goal.LastMilestoneNotified = Math.Min(
            goal.LastMilestoneNotified,
            SavingsGoalProjectionCalculator.MilestoneFor(
                progress.AmountSaved,
                progress.TargetAmount));
        goal.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToResponse(goal, progress, canEdit: true);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var goal = await FindEditableAsync(id, userId, ct);

        if (goal is null)
            return false;

        _db.SavingsGoals.Remove(goal);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<SavingsGoalContributionResponse>?> GetContributionsAsync(
        Guid goalId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (await FindAccessibleAsync(goalId, userId, ct) is null)
            return null;

        return await (
            from contribution in _db.SavingsGoalContributions.AsNoTracking()
            where contribution.SavingsGoalId == goalId
            join user in _db.Users.AsNoTracking()
                on contribution.UserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            orderby contribution.ContributionDateUtc descending,
                contribution.CreatedAtUtc descending
            select new SavingsGoalContributionResponse(
                contribution.Id,
                contribution.SavingsGoalId,
                contribution.Amount,
                contribution.ContributionDateUtc,
                contribution.Note,
                contribution.CreatedAtUtc,
                contribution.UserId,
                user == null ? null : user.Email,
                contribution.ScheduledOccurrenceUtc != null))
            .ToListAsync(ct);
    }

    public async Task<SavingsGoalResponse?> AddContributionAsync(
        Guid goalId,
        Guid userId,
        SavingsGoalContributionRequest request,
        CancellationToken ct = default)
    {
        ValidateAmount(request.Amount, "Contribution");

        if (request.ContributionDateUtc.Kind != DateTimeKind.Utc ||
            request.ContributionDateUtc.Date > DateTime.UtcNow.Date)
        {
            throw new ArgumentException(
                "Contribution date must be a UTC date and time that is not in the future.");
        }

        if (request.Note?.Trim().Length > 250)
            throw new ArgumentException("Contribution note cannot exceed 250 characters.");

        var goal = await FindEditableAsync(goalId, userId, ct);

        if (goal is null)
            return null;

        if (goal.LinkedAccountId is not null)
        {
            throw new ArgumentException(
                "This goal tracks its linked account balance. Deposit into that account instead.");
        }

        _db.SavingsGoalContributions.Add(new SavingsGoalContribution
        {
            UserId = userId,
            SavingsGoalId = goalId,
            Amount = request.Amount,
            ContributionDateUtc = request.ContributionDateUtc,
            Note = request.Note?.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        });

        goal.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return ToResponse(goal, await ReadProgressAsync(goal, ct), canEdit: true);
    }

    public async Task<bool> DeleteContributionAsync(
        Guid goalId,
        Guid contributionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var goal = await FindEditableAsync(goalId, userId, ct);

        if (goal is null)
            return false;

        var contribution = await _db.SavingsGoalContributions
            .SingleOrDefaultAsync(
                x => x.Id == contributionId && x.SavingsGoalId == goalId,
                ct);

        if (contribution is null)
            return false;

        _db.SavingsGoalContributions.Remove(contribution);
        goal.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Returns null when the goal is not visible to the user and throws
    // when it is visible but the user may not change it.
    private async Task<SavingsGoal?> FindEditableAsync(
        Guid id,
        Guid userId,
        CancellationToken ct)
    {
        var goal = await FindAccessibleAsync(id, userId, ct, tracking: true);

        if (goal is not null && !await CanEditAsync(goal, userId, ct))
        {
            throw new UnauthorizedAccessException(
                "Your household role does not allow shared savings goal changes.");
        }

        return goal;
    }

    private async Task<SavingsGoal?> FindAccessibleAsync(
        Guid id,
        Guid userId,
        CancellationToken ct,
        bool tracking = false)
    {
        var householdIds = await _householdAuthorization
            .GetHouseholdIdsAsync(userId, ct);

        var query = tracking
            ? _db.SavingsGoals
            : _db.SavingsGoals.AsNoTracking();

        return await query.SingleOrDefaultAsync(
            x => x.Id == id &&
                 (x.UserId == userId ||
                  (x.HouseholdId.HasValue &&
                   householdIds.Contains(x.HouseholdId.Value))),
            ct);
    }

    private async Task<bool> CanEditAsync(
        SavingsGoal goal,
        Guid userId,
        CancellationToken ct)
    {
        return goal.HouseholdId is { } householdId
            ? await _householdAuthorization.CanEditAsync(userId, householdId, ct)
            : goal.UserId == userId;
    }

    private async Task<SavingsGoalProgress> ReadProgressAsync(
        SavingsGoal goal,
        CancellationToken ct)
    {
        var progress = await _progress.ReadAsync([goal], DateTime.UtcNow, ct);
        return progress[goal.Id];
    }

    // Sets how progress is tracked: a linked account balance, or
    // manual contributions with an optional schedule. Not both, since
    // scheduled contributions would double count account deposits.
    private async Task ApplyTrackingAsync(
        SavingsGoal goal,
        Guid? linkedAccountId,
        decimal? autoAmount,
        RecurrenceFrequency? autoFrequency,
        DateTime? autoStartUtc,
        CancellationToken ct)
    {
        var hasSchedule = autoAmount is not null || autoFrequency is not null;

        if (linkedAccountId is { } accountId)
        {
            if (hasSchedule)
            {
                throw new ArgumentException(
                    "Scheduled contributions are not available for goals that track a linked account.");
            }

            await ValidateLinkedAccountAsync(goal, accountId, ct);
        }

        goal.LinkedAccountId = linkedAccountId;

        if (!hasSchedule)
        {
            goal.AutoContributionAmount = null;
            goal.AutoContributionFrequency = null;
            goal.AutoContributionStartUtc = null;
            goal.NextAutoContributionUtc = null;
            return;
        }

        if (autoAmount is not { } amount || autoFrequency is not { } frequency)
        {
            throw new ArgumentException(
                "Scheduled contributions need both an amount and a frequency.");
        }

        ValidateAmount(amount, "Scheduled contribution");

        if (!Enum.IsDefined(frequency))
            throw new ArgumentException("Invalid contribution frequency.");

        var now = DateTime.UtcNow;
        var start = autoStartUtc ?? now;

        if (start.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Scheduled contribution start date must be UTC.");

        var unchanged =
            goal.AutoContributionAmount == amount &&
            goal.AutoContributionFrequency == frequency &&
            goal.AutoContributionStartUtc == start &&
            goal.NextAutoContributionUtc is not null;

        goal.AutoContributionAmount = amount;
        goal.AutoContributionFrequency = frequency;
        goal.AutoContributionStartUtc = start;

        if (unchanged)
            return;

        // A start date of today or later posts on that date. An earlier
        // date only anchors the schedule; missed dates are not back-filled.
        goal.NextAutoContributionUtc = start.Date >= now.Date
            ? start
            : RecurrenceCalculator.GetNextOccurrence(
                start,
                frequency,
                now,
                await _progress.GetTimeZoneIdAsync(goal.UserId, ct));
    }

    private async Task ValidateLinkedAccountAsync(
        SavingsGoal goal,
        Guid accountId,
        CancellationToken ct)
    {
        var account = await _db.FinancialAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == accountId, ct);

        var inScope = account is not null &&
            (goal.HouseholdId is { } householdId
                ? account.HouseholdId == householdId
                : account.UserId == goal.UserId);

        if (!inScope)
        {
            throw new ArgumentException(goal.HouseholdId is null
                ? "Choose one of your own accounts to link."
                : "Choose an account shared with this household to link.");
        }

        if (account!.Currency != goal.Currency)
            throw new ArgumentException("The linked account must use the goal's currency.");

        if (await _db.SavingsGoals.AnyAsync(
                x => x.LinkedAccountId == accountId && x.Id != goal.Id,
                ct))
        {
            throw new ArgumentException(
                "That account is already linked to another savings goal.");
        }
    }

    private async Task EnsureUserHasCurrencyAccountAsync(
        Guid userId,
        string currency,
        CancellationToken ct)
    {
        if (!await _db.FinancialAccounts.AnyAsync(
                x => x.UserId == userId && x.Currency == currency,
                ct))
        {
            throw new ArgumentException(
                "Create an account in this currency before adding its savings goal.");
        }
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150)
            throw new ArgumentException("Goal name is required and cannot exceed 150 characters.");
    }

    private static void ValidateAmount(decimal amount, string label)
    {
        if (amount <= 0 ||
            amount > MaximumAmount ||
            decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException(
                $"{label} must be positive, no greater than 16 digits, and have at most two decimal places.");
        }
    }

    private static void ValidateTargetDate(DateTime? targetDateUtc)
    {
        if (targetDateUtc is not null &&
            targetDateUtc.Value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Target date must be UTC.");
        }
    }

    private static void ValidateGoalType(
        SavingsGoalType goalType,
        int? emergencyFundMonths)
    {
        switch (goalType)
        {
            case SavingsGoalType.Custom when emergencyFundMonths is not null:
                throw new ArgumentException(
                    "Months of expenses only apply to emergency fund goals.");

            case SavingsGoalType.EmergencyFund
                when emergencyFundMonths is not (>= 1 and <= MaximumEmergencyFundMonths):
                throw new ArgumentException(
                    $"Emergency funds must cover between 1 and {MaximumEmergencyFundMonths} months of expenses.");

            case SavingsGoalType.Custom:
            case SavingsGoalType.EmergencyFund:
                return;

            default:
                throw new ArgumentException("Invalid savings goal type.");
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
            throw new ArgumentException("Enter a valid three-letter currency code.");

        return normalized;
    }

    private static SavingsGoalResponse ToResponse(
        SavingsGoal goal,
        SavingsGoalProgress progress,
        bool canEdit)
    {
        var target = progress.TargetAmount;
        var saved = progress.AmountSaved;
        var projection = SavingsGoalProjectionCalculator.Project(
            target,
            saved,
            progress.MonthlyPace,
            goal.TargetDateUtc,
            DateTime.UtcNow);

        return new SavingsGoalResponse(
            goal.Id,
            goal.Name,
            goal.Currency,
            target,
            saved,
            Math.Max(0, target - saved),
            Math.Clamp(
                Math.Round(
                    saved / target * 100,
                    2,
                    MidpointRounding.AwayFromZero),
                0,
                100),
            goal.TargetDateUtc,
            goal.CreatedAtUtc,
            goal.UpdatedAtUtc,
            goal.HouseholdId,
            canEdit,
            goal.GoalType,
            goal.EmergencyFundMonths,
            progress.AverageMonthlyExpenses,
            goal.LinkedAccountId,
            progress.LinkedAccountName,
            goal.AutoContributionAmount,
            goal.AutoContributionFrequency,
            goal.AutoContributionStartUtc,
            goal.NextAutoContributionUtc,
            goal.NotificationsEnabled,
            projection.Status,
            progress.MonthlyPace,
            projection.MonthlyContributionNeeded,
            projection.ProjectedCompletionDateUtc);
    }
}
