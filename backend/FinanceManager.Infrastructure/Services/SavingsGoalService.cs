using FinanceManager.Application.DTOs.SavingsGoals;
using FinanceManager.Application.Interfaces;
using FinanceManager.Domain.Entities;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public sealed class SavingsGoalService : ISavingsGoalService
{
    private const decimal MaximumAmount = 9999999999999999.99m;

    private readonly FinanceDbContext _db;

    public SavingsGoalService(FinanceDbContext db)
    {
        _db = db;
    }

    public async Task<List<SavingsGoalResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var goals = await _db.SavingsGoals
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var totals = await _db.SavingsGoalContributions
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .GroupBy(x => x.SavingsGoalId)
            .Select(x => new
            {
                GoalId = x.Key,
                Total = x.Sum(y => y.Amount)
            })
            .ToDictionaryAsync(x => x.GoalId, x => x.Total, ct);

        return goals.Select(goal =>
            ToResponse(goal, totals.GetValueOrDefault(goal.Id))).ToList();
    }

    public async Task<SavingsGoalResponse> CreateAsync(
        Guid userId,
        SavingsGoalUpsertRequest request,
        CancellationToken ct = default)
    {
        ValidateName(request.Name);
        ValidateAmount(request.TargetAmount, "Target amount");
        ValidateTargetDate(request.TargetDateUtc);

        var currency = NormalizeCurrency(request.Currency);
        await EnsureUserHasCurrencyAccountAsync(userId, currency, ct);

        var now = DateTime.UtcNow;
        var goal = new SavingsGoal
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Currency = currency,
            TargetAmount = request.TargetAmount,
            TargetDateUtc = request.TargetDateUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.SavingsGoals.Add(goal);
        await _db.SaveChangesAsync(ct);

        return ToResponse(goal, 0);
    }

    public async Task<SavingsGoalResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        SavingsGoalUpdateRequest request,
        CancellationToken ct = default)
    {
        var goal = await _db.SavingsGoals
            .SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                ct);

        if (goal is null)
            return null;

        ValidateName(request.Name);
        ValidateAmount(request.TargetAmount, "Target amount");
        ValidateTargetDate(request.TargetDateUtc);

        goal.Name = request.Name.Trim();
        goal.TargetAmount = request.TargetAmount;
        goal.TargetDateUtc = request.TargetDateUtc;
        goal.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var saved = await GetSavedAmountAsync(id, userId, ct);
        return ToResponse(goal, saved);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var goal = await _db.SavingsGoals
            .SingleOrDefaultAsync(
                x => x.Id == id && x.UserId == userId,
                ct);

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
        var goalExists = await _db.SavingsGoals
            .AnyAsync(x => x.Id == goalId && x.UserId == userId, ct);

        if (!goalExists)
            return null;

        return await _db.SavingsGoalContributions
            .AsNoTracking()
            .Where(x => x.SavingsGoalId == goalId && x.UserId == userId)
            .OrderByDescending(x => x.ContributionDateUtc)
            .Select(x => ToContributionResponse(x))
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

        var goal = await _db.SavingsGoals
            .SingleOrDefaultAsync(
                x => x.Id == goalId && x.UserId == userId,
                ct);

        if (goal is null)
            return null;

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

        var saved = await GetSavedAmountAsync(goalId, userId, ct);
        return ToResponse(goal, saved);
    }

    public async Task<bool> DeleteContributionAsync(
        Guid goalId,
        Guid contributionId,
        Guid userId,
        CancellationToken ct = default)
    {
        var contribution = await _db.SavingsGoalContributions
            .SingleOrDefaultAsync(
                x => x.Id == contributionId &&
                     x.SavingsGoalId == goalId &&
                     x.UserId == userId,
                ct);

        if (contribution is null)
            return false;

        _db.SavingsGoalContributions.Remove(contribution);

        var goal = await _db.SavingsGoals
            .SingleAsync(x => x.Id == goalId && x.UserId == userId, ct);
        goal.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<decimal> GetSavedAmountAsync(
        Guid goalId,
        Guid userId,
        CancellationToken ct)
    {
        return await _db.SavingsGoalContributions
            .Where(x => x.SavingsGoalId == goalId && x.UserId == userId)
            .SumAsync(x => (decimal?)x.Amount, ct)
            ?? 0;
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

    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length != 3)
            throw new ArgumentException("Enter a valid three-letter currency code.");

        return normalized;
    }

    private static SavingsGoalResponse ToResponse(
        SavingsGoal goal,
        decimal amountSaved)
    {
        return new SavingsGoalResponse(
            goal.Id,
            goal.Name,
            goal.Currency,
            goal.TargetAmount,
            amountSaved,
            Math.Max(0, goal.TargetAmount - amountSaved),
            Math.Min(
                100,
                Math.Round(
                    amountSaved / goal.TargetAmount * 100,
                    2,
                    MidpointRounding.AwayFromZero)),
            goal.TargetDateUtc,
            goal.CreatedAtUtc,
            goal.UpdatedAtUtc);
    }

    private static SavingsGoalContributionResponse ToContributionResponse(
        SavingsGoalContribution contribution)
    {
        return new SavingsGoalContributionResponse(
            contribution.Id,
            contribution.SavingsGoalId,
            contribution.Amount,
            contribution.ContributionDateUtc,
            contribution.Note,
            contribution.CreatedAtUtc);
    }
}
