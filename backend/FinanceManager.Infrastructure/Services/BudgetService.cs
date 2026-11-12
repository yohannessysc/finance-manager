using FinanceManager.Application.DTOs.Budgets;
using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public sealed class BudgetService : IBudgetService
{
    private const string DefaultTimeZoneId = "America/Toronto";
    private const decimal MaximumLimit = 9999999999999999.99m;

    private readonly FinanceDbContext _db;
    private readonly IHouseholdAuthorizationService _householdAuthorization;

    public BudgetService(
        FinanceDbContext db,
        IHouseholdAuthorizationService householdAuthorization)
    {
        _db = db;
        _householdAuthorization = householdAuthorization;
    }

    public async Task<List<BudgetResponse>> GetAllAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var householdIds = await _householdAuthorization
            .GetHouseholdIdsAsync(userId, ct);

        var budgets = await (
            from budget in _db.MonthlyBudgets.AsNoTracking()
            join category in _db.TransactionCategories.AsNoTracking()
                on budget.CategoryId equals category.Id
            where budget.UserId == userId ||
                  (budget.HouseholdId.HasValue &&
                   householdIds.Contains(budget.HouseholdId.Value))
            orderby category.Name, budget.Currency
            select new
            {
                Budget = budget,
                CategoryName = category.Name
            }).ToListAsync(ct);

        var spending = await GetSpendingAsync(
            userId,
            householdIds,
            ct);

        var results = new List<BudgetResponse>(budgets.Count);
        foreach (var item in budgets)
        {
            var period = await GetPeriodAsync(
                userId,
                item.Budget.HouseholdId,
                ct);
            var canEdit = item.Budget.HouseholdId is null
                ? item.Budget.UserId == userId
                : await _householdAuthorization.CanEditAsync(
                    userId,
                    item.Budget.HouseholdId.Value,
                    ct);

            results.Add(ToResponse(
                item.Budget,
                item.CategoryName,
                spending.GetValueOrDefault((
                    item.Budget.HouseholdId,
                    item.Budget.CategoryId,
                    item.Budget.Currency)),
                period.StartUtc,
                period.EndUtc,
                period.StartDate,
                period.EndDate,
                canEdit));
        }

        return results;
    }

    public async Task<BudgetResponse> CreateAsync(
        Guid userId,
        BudgetUpsertRequest request,
        CancellationToken ct = default)
    {
        var (category, currency, limit) = await ValidateAsync(
            userId,
            request,
            excludedId: null,
            ct);

        var budget = new MonthlyBudget
        {
            UserId = userId,
            HouseholdId = request.HouseholdId,
            CategoryId = category.Id,
            Currency = currency,
            MonthlyLimit = limit,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.MonthlyBudgets.Add(budget);
        await _db.SaveChangesAsync(ct);

        var period = await GetPeriodAsync(
            userId,
            request.HouseholdId,
            ct);
        var spending = await GetSpendingAsync(
            userId,
            await _householdAuthorization.GetHouseholdIdsAsync(userId, ct),
            ct);

        return ToResponse(
            budget,
            category.Name,
            spending.GetValueOrDefault((
                request.HouseholdId,
                category.Id,
                currency)),
            period.StartUtc,
            period.EndUtc,
            period.StartDate,
            period.EndDate,
            canEdit: true);
    }

    public async Task<BudgetResponse?> UpdateAsync(
        Guid id,
        Guid userId,
        BudgetUpsertRequest request,
        CancellationToken ct = default)
    {
        var householdIds = await _householdAuthorization
            .GetHouseholdIdsAsync(userId, ct);
        var budget = await _db.MonthlyBudgets
            .SingleOrDefaultAsync(
                x => x.Id == id &&
                     (x.UserId == userId ||
                      (x.HouseholdId.HasValue &&
                       householdIds.Contains(x.HouseholdId.Value))),
                ct);

        if (budget is null)
            return null;
        if (budget.HouseholdId is null && budget.UserId != userId)
            return null;
        if (budget.HouseholdId.HasValue &&
            (!await _householdAuthorization.CanEditAsync(
                 userId,
                 budget.HouseholdId.Value,
                 ct) ||
             budget.HouseholdId != request.HouseholdId))
        {
            return null;
        }

        var (category, currency, limit) = await ValidateAsync(
            userId,
            request,
            excludedId: id,
            ct);

        budget.CategoryId = category.Id;
        budget.HouseholdId = request.HouseholdId;
        budget.Currency = currency;
        budget.MonthlyLimit = limit;
        budget.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        var period = await GetPeriodAsync(
            userId,
            request.HouseholdId,
            ct);
        var spending = await GetSpendingAsync(
            userId,
            householdIds,
            ct);

        return ToResponse(
            budget,
            category.Name,
            spending.GetValueOrDefault((
                request.HouseholdId,
                category.Id,
                currency)),
            period.StartUtc,
            period.EndUtc,
            period.StartDate,
            period.EndDate,
            canEdit: true);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken ct = default)
    {
        var householdIds = await _householdAuthorization
            .GetHouseholdIdsAsync(userId, ct);
        var budget = await _db.MonthlyBudgets
            .SingleOrDefaultAsync(
                x => x.Id == id &&
                     (x.UserId == userId ||
                      (x.HouseholdId.HasValue &&
                       householdIds.Contains(x.HouseholdId.Value))),
                ct);

        if (budget is null)
            return false;
        if (budget.HouseholdId is null && budget.UserId != userId)
            return false;
        if (budget.HouseholdId.HasValue &&
            !await _householdAuthorization.CanEditAsync(
                userId,
                budget.HouseholdId.Value,
                ct))
        {
            return false;
        }

        _db.MonthlyBudgets.Remove(budget);
        await _db.SaveChangesAsync(ct);

        return true;
    }

    private async Task<(TransactionCategory Category, string Currency, decimal Limit)>
        ValidateAsync(
            Guid userId,
            BudgetUpsertRequest request,
            Guid? excludedId,
            CancellationToken ct)
    {
        if (request.MonthlyLimit <= 0 ||
            request.MonthlyLimit > MaximumLimit ||
            decimal.Round(request.MonthlyLimit, 2) != request.MonthlyLimit)
        {
            throw new ArgumentException(
                "Monthly limit must be positive, no greater than 16 digits, and have at most two decimal places.");
        }

        var currency = request.Currency?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Enter a valid three-letter currency code.");

        var categoryQuery = _db.TransactionCategories
            .Where(x =>
                x.Id == request.CategoryId &&
                x.Type == TransactionType.Expense);
        if (request.HouseholdId is null)
        {
            categoryQuery = categoryQuery.Where(x => x.UserId == userId);
        }
        else
        {
            if (!await _householdAuthorization.CanEditAsync(
                    userId,
                    request.HouseholdId.Value,
                    ct))
            {
                throw new UnauthorizedAccessException(
                    "Your household role does not allow shared budget changes.");
            }

            var householdMemberIds = _db.HouseholdMembers
                .Where(x => x.HouseholdId == request.HouseholdId.Value)
                .Select(x => x.UserId);
            categoryQuery = categoryQuery.Where(
                x => householdMemberIds.Contains(x.UserId));
        }

        var category = await categoryQuery.SingleOrDefaultAsync(ct);

        if (category is null)
            throw new ArgumentException(
                request.HouseholdId is null
                    ? "Choose one of your expense categories."
                    : "Choose an expense category belonging to a household member.");

        var requestedHouseholdId = request.HouseholdId;
        var hasSharedScope = requestedHouseholdId.HasValue;
        var hasCurrencyAccount = await _db.FinancialAccounts
            .AnyAsync(
                x => x.Currency == currency &&
                     (hasSharedScope
                         ? x.HouseholdId == requestedHouseholdId
                         : x.UserId == userId && x.HouseholdId == null),
                ct);

        if (!hasCurrencyAccount)
        {
            throw new ArgumentException(
                request.HouseholdId is null
                    ? "Create a private account in this currency before adding its budget."
                    : "Share an account in this currency with the household before adding its budget.");
        }

        var duplicate = await _db.MonthlyBudgets
            .AnyAsync(
                x => (hasSharedScope
                         ? x.HouseholdId == requestedHouseholdId
                         : x.UserId == userId && x.HouseholdId == null) &&
                     x.CategoryId == category.Id &&
                     x.Currency == currency &&
                     x.Id != excludedId,
                ct);

        if (duplicate)
        {
            throw new ArgumentException(
                "A budget already exists for this category and currency.");
        }

        return (category, currency, request.MonthlyLimit);
    }

    private async Task<(
        DateTime StartUtc,
        DateTime EndUtc,
        DateOnly StartDate,
        DateOnly EndDate)> GetPeriodAsync(
        Guid userId,
        Guid? householdId,
        CancellationToken ct)
    {
        var preferenceUserId = householdId.HasValue
            ? await _db.Households
                .Where(x => x.Id == householdId.Value)
                .Select(x => x.OwnerUserId)
                .SingleAsync(ct)
            : userId;
        var preference = await _db.UserPreferences
            .AsNoTracking()
            .Where(x => x.UserId == preferenceUserId)
            .Select(x => new { x.BudgetStartDay, x.TimeZone })
            .SingleOrDefaultAsync(ct);

        var timeZoneId = preference?.TimeZone ?? DefaultTimeZoneId;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var period = BudgetCycleCalculator.GetCurrentPeriod(
            DateTime.UtcNow,
            preference?.BudgetStartDay ?? 1,
            timeZoneId);
        return (
            period.StartUtc,
            period.EndUtc,
            DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    period.StartUtc,
                    timeZone)),
            DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    period.EndUtc.AddTicks(-1),
                    timeZone)));
    }

    private async Task<Dictionary<
        (Guid? HouseholdId, Guid? CategoryId, string Currency),
        decimal>>
        GetSpendingAsync(
            Guid userId,
            IReadOnlyCollection<Guid> householdIds,
            CancellationToken ct)
    {
        var scopePeriods = new List<(Guid? HouseholdId, DateTime StartUtc, DateTime EndUtc)>();
        var nowUtc = DateTime.UtcNow;
        var personalPeriod = await GetPeriodAsync(userId, null, ct);
        scopePeriods.Add((null, personalPeriod.StartUtc, personalPeriod.EndUtc));
        foreach (var householdId in householdIds)
        {
            var period = await GetPeriodAsync(userId, householdId, ct);
            scopePeriods.Add((householdId, period.StartUtc, period.EndUtc));
        }

        var earliest = scopePeriods.Min(x => x.StartUtc);
        var latest = scopePeriods.Max(x => x.EndUtc);

        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(account => account.HouseholdId.HasValue
                ? householdIds.Contains(account.HouseholdId.Value)
                : account.UserId == userId)
            .Select(account => new { account.Id, account.HouseholdId, account.Currency })
            .ToDictionaryAsync(account => account.Id, ct);
        var accountIds = accounts.Keys.ToList();

        var lines = await CategoryLineReader.ReadAsync(
            _db,
            _db.FinancialTransactions.AsNoTracking().Where(transaction =>
                transaction.Type == TransactionType.Expense &&
                transaction.TransactionDateUtc <= nowUtc &&
                transaction.TransactionDateUtc >= earliest &&
                transaction.TransactionDateUtc < latest &&
                accountIds.Contains(transaction.FinancialAccountId)),
            ct);

        var categories = await CategoryLineReader.ReadCategoriesAsync(
            _db,
            lines.Select(line => line.CategoryId),
            ct);

        var spending = new Dictionary<(Guid? HouseholdId, Guid? CategoryId, string Currency), decimal>();
        foreach (var line in lines)
        {
            var account = accounts[line.AccountId];
            var inPeriod = scopePeriods.Any(scope =>
                scope.HouseholdId == account.HouseholdId &&
                line.DateUtc >= scope.StartUtc &&
                line.DateUtc < scope.EndUtc);
            if (!inPeriod)
                continue;

            Add(line.CategoryId);

            // A parent category's budget also covers its subcategories.
            if (line.CategoryId is { } categoryId &&
                categories.TryGetValue(categoryId, out var category) &&
                category.ParentId is { } parentId)
            {
                Add(parentId);
            }

            void Add(Guid? id)
            {
                var key = (account.HouseholdId, id, account.Currency);
                spending[key] = spending.GetValueOrDefault(key) + line.Amount;
            }
        }

        return spending;
    }

    private static BudgetResponse ToResponse(
        MonthlyBudget budget,
        string categoryName,
        decimal spent,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        DateOnly periodStartDate,
        DateOnly periodEndDate,
        bool canEdit)
    {
        return new BudgetResponse(
            budget.Id,
            budget.CategoryId,
            categoryName,
            budget.Currency,
            budget.MonthlyLimit,
            spent,
            budget.MonthlyLimit - spent,
            Math.Round(
                spent / budget.MonthlyLimit * 100,
                2,
                MidpointRounding.AwayFromZero),
            periodStartUtc,
            periodEndUtc,
            periodStartDate,
            periodEndDate,
            budget.HouseholdId,
            canEdit);
    }
}
