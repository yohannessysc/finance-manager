
using FinanceManager.Application.DTOs.Dashboard;
using FinanceManager.Application.DTOs.Transactions;
using FinanceManager.Application.Interfaces;
using FinanceManager.Application.Services;
using FinanceManager.Domain.Enums;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private const string DefaultTimeZoneId = "America/Toronto";

    private readonly FinanceDbContext _db;
    private readonly IAccountBalanceService _accountBalanceService;

    public DashboardService(
        FinanceDbContext db,
        IAccountBalanceService accountBalanceService)
    {
        _db = db;
        _accountBalanceService = accountBalanceService;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var timeZone = await GetUserTimeZoneAsync(userId, ct);

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(
            nowUtc, timeZone);

        var currentMonth = new DateTime(
            localNow.Year, localNow.Month, 1);

        var firstMonth = currentMonth.AddMonths(-5);
        var nextMonth = currentMonth.AddMonths(1);

        var rangeStartUtc = ToUtc(firstMonth, timeZone);
        var rangeEndUtc = ToUtc(nextMonth, timeZone);
        var currentMonthStartUtc = ToUtc(currentMonth, timeZone);

        // 1. Current account balances.
        var balances = await _accountBalanceService.GetAllAsync(
            userId, ct);

        // Account lookup also provides the currency for transactions.
        var accountCurrencies = balances.ToDictionary(
            account => account.AccountId,
            account => account.Currency);
        var accessibleAccountIds = accountCurrencies.Keys.ToArray();

        // 2. Transactions for the last six calendar months.
        var transactions = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t =>
                accessibleAccountIds.Contains(t.FinancialAccountId) &&
                t.TransactionDateUtc >= rangeStartUtc &&
                t.TransactionDateUtc <= nowUtc &&
                t.TransactionDateUtc < rangeEndUtc)
            .ToListAsync(ct);

        // 3. Income/expense trends grouped by month and currency.
        var monthlyCashFlow = new List<MonthlyCashFlowResponse>();

        for (var i = 0; i < 6; i++)
        {
            var monthStart = firstMonth.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1);

            var startUtc = ToUtc(monthStart, timeZone);
            var endUtc = ToUtc(monthEnd, timeZone);

            var monthTransactions = transactions
                .Where(t =>
                    t.TransactionDateUtc >= startUtc &&
                    t.TransactionDateUtc < endUtc)
                .Where(t =>
                    t.Type == TransactionType.Income ||
                    t.Type == TransactionType.Expense)
                .Where(t =>
                    accountCurrencies.ContainsKey(
                        t.FinancialAccountId))
                .ToList();

            var currencies = accountCurrencies.Values
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            foreach (var currency in currencies)
            {
                var currencyTransactions = monthTransactions
                    .Where(t =>
                        string.Equals(
                            accountCurrencies[t.FinancialAccountId],
                            currency,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var income = currencyTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .Sum(t => t.Amount);

                var expenses = currencyTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .Sum(t => t.Amount);

                monthlyCashFlow.Add(
                    new MonthlyCashFlowResponse(
                        monthStart.Year,
                        monthStart.Month,
                        currency,
                        income,
                        expenses,
                        income - expenses));
            }
        }

        // 4. Current-month spending grouped by top-level category
        // (split lines count separately; subcategories roll up).
        var currentLines = (await CategoryLineReader.ReadAsync(
                _db,
                _db.FinancialTransactions.AsNoTracking().Where(t =>
                    accessibleAccountIds.Contains(t.FinancialAccountId) &&
                    t.TransactionDateUtc >= currentMonthStartUtc &&
                    t.TransactionDateUtc <= nowUtc &&
                    t.TransactionDateUtc < rangeEndUtc &&
                    t.Type == TransactionType.Expense),
                ct))
            .Where(line => accountCurrencies.ContainsKey(line.AccountId))
            .ToList();

        var categoryInfo = await CategoryLineReader.ReadCategoriesAsync(
            _db,
            currentLines.Select(line => line.CategoryId),
            ct);
        var categories = categoryInfo.ToDictionary(x => x.Key, x => x.Value.Name);

        Guid? TopLevel(Guid? id) =>
            id is { } value &&
            categoryInfo.TryGetValue(value, out var info) &&
            info.ParentId is { } parentId
                ? parentId
                : id;

        var groupedExpenses = currentLines
            .GroupBy(line => new
            {
                Currency = accountCurrencies[line.AccountId],
                CategoryId = TopLevel(line.CategoryId)
            })
            .Select(g => new
            {
                g.Key.Currency,
                g.Key.CategoryId,
                Amount = g.Sum(line => line.Amount)
            })
            .ToList();

        var spendingByCategory = groupedExpenses
            .Select(group =>
            {
                var currencyTotal = groupedExpenses
                    .Where(g => string.Equals(
                        g.Currency,
                        group.Currency,
                        StringComparison.OrdinalIgnoreCase))
                    .Sum(g => g.Amount);

                var percentage = currencyTotal == 0
                    ? 0
                    : Math.Round(
                        group.Amount / currencyTotal * 100,
                        2);

                var categoryName = "Uncategorized";

                if (group.CategoryId.HasValue &&
                    categories.TryGetValue(
                        group.CategoryId.Value,
                        out var name))
                {
                    categoryName = name;
                }

                return new CategorySpendingResponse(
                    group.CategoryId,
                    categoryName,
                    group.Currency,
                    group.Amount,
                    percentage);
            })
            .OrderBy(x => x.Currency)
            .ThenByDescending(x => x.Amount)
            .ToList();

        // 5. Ten most recent transactions.
        var recentEntities = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t =>
                accessibleAccountIds.Contains(t.FinancialAccountId) &&
                t.TransactionDateUtc <= nowUtc)
            .OrderByDescending(t => t.TransactionDateUtc)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Take(10)
            .ToListAsync(ct);

        var recentTransactions = recentEntities
            .Select(t => new TransactionResponse(
                t.Id,
                t.FinancialAccountId,
                t.DestinationAccountId,
                t.CategoryId,
                t.RecurringTransactionId,
                t.Type,
                t.Amount,
                t.Description,
                t.Merchant,
                t.TransactionDateUtc,
                t.CreatedAtUtc))
            .ToList();

        // 6. Upcoming active recurring transactions.
        var recurringEntities = await _db.RecurringTransactions
            .AsNoTracking()
            .Where(r =>
                accessibleAccountIds.Contains(r.FinancialAccountId) &&
                r.Status == RecurringStatus.Active &&
                r.NextOccurrenceUtc <= nowUtc.AddDays(30) &&
                (r.EndDateUtc == null ||
                 r.NextOccurrenceUtc <= r.EndDateUtc))
            .OrderBy(r => r.NextOccurrenceUtc)
            .Take(10)
            .ToListAsync(ct);

        var upcomingRecurring = recurringEntities
            .Where(r => accountCurrencies.ContainsKey(
                r.FinancialAccountId))
            .Select(r => new UpcomingRecurringResponse(
                r.Id,
                r.Description,
                r.Type,
                r.Amount,
                accountCurrencies[r.FinancialAccountId],
                r.NextOccurrenceUtc,
                r.TimeZoneId))
            .ToList();

        return new DashboardSummaryResponse(
            balances,
            monthlyCashFlow,
            spendingByCategory,
            recentTransactions,
            upcomingRecurring);
    }

    public async Task<CashFlowForecastResponse> GetForecastAsync(
        Guid userId,
        int horizonDays,
        CancellationToken ct = default)
    {
        if (horizonDays is < 1 or > 365)
        {
            throw new ArgumentOutOfRangeException(
                nameof(horizonDays),
                "Forecast horizon must be between 1 and 365 days.");
        }

        var nowUtc = DateTime.UtcNow;
        var timeZone = await GetUserTimeZoneAsync(userId, ct);
        var localToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone));
        var endDate = localToday.AddDays(horizonDays);
        var forecastEndUtc = ToUtc(
            endDate.ToDateTime(TimeOnly.MinValue),
            timeZone);

        var balances = await _accountBalanceService.GetAllAsync(
            userId,
            ct);
        var accountCurrencies = balances.ToDictionary(
            x => x.AccountId,
            x => x.Currency);
        var accessibleAccountIds = accountCurrencies.Keys.ToArray();
        var startingBalances = balances
            .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(account => account.CurrentBalance),
                StringComparer.OrdinalIgnoreCase);

        var events = new Dictionary<
            (string Currency, DateOnly Date),
            ForecastEvent>();

        void AddEvent(
            string currency,
            DateTime eventUtc,
            TransactionType type,
            decimal amount)
        {
            eventUtc = DateTime.SpecifyKind(
                eventUtc,
                DateTimeKind.Utc);
            var localDate = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    eventUtc < nowUtc ? nowUtc : eventUtc,
                    timeZone));

            if (localDate < localToday || localDate >= endDate)
                return;

            var key = (currency, localDate);
            if (!events.TryGetValue(key, out var forecastEvent))
            {
                forecastEvent = new ForecastEvent();
                events.Add(key, forecastEvent);
            }

            if (type == TransactionType.Income)
                forecastEvent.Income += amount;
            else if (type == TransactionType.Expense)
                forecastEvent.Expenses += amount;

            forecastEvent.PlannedItems++;
        }

        var upcomingTransactions = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t =>
                accessibleAccountIds.Contains(t.FinancialAccountId) &&
                t.TransactionDateUtc > nowUtc &&
                t.TransactionDateUtc < forecastEndUtc &&
                (t.Type == TransactionType.Income ||
                 t.Type == TransactionType.Expense))
            .ToListAsync(ct);

        foreach (var transaction in upcomingTransactions)
        {
            if (!accountCurrencies.TryGetValue(
                transaction.FinancialAccountId,
                out var currency))
            {
                continue;
            }

            AddEvent(
                currency,
                transaction.TransactionDateUtc,
                transaction.Type,
                transaction.Amount);
        }

        var schedules = await _db.RecurringTransactions
            .AsNoTracking()
            .Where(r =>
                accessibleAccountIds.Contains(r.FinancialAccountId) &&
                r.Status == RecurringStatus.Active &&
                r.NextOccurrenceUtc < forecastEndUtc &&
                (r.Type == TransactionType.Income ||
                 r.Type == TransactionType.Expense) &&
                (r.EndDateUtc == null ||
                 r.NextOccurrenceUtc <= r.EndDateUtc))
            .ToListAsync(ct);

        foreach (var schedule in schedules)
        {
            if (!accountCurrencies.TryGetValue(
                schedule.FinancialAccountId,
                out var currency))
            {
                continue;
            }

            var occurrence = DateTime.SpecifyKind(
                schedule.NextOccurrenceUtc,
                DateTimeKind.Utc);
            var startDateUtc = DateTime.SpecifyKind(
                schedule.StartDateUtc,
                DateTimeKind.Utc);
            var endDateUtc = schedule.EndDateUtc.HasValue
                ? DateTime.SpecifyKind(
                    schedule.EndDateUtc.Value,
                    DateTimeKind.Utc)
                : (DateTime?)null;

            if (occurrence < nowUtc)
            {
                AddEvent(
                    currency,
                    occurrence,
                    schedule.Type,
                    schedule.Amount);

                occurrence = RecurrenceCalculator.GetNextOccurrence(
                    startDateUtc,
                    schedule.Frequency,
                    nowUtc,
                    schedule.TimeZoneId);
            }

            while (occurrence < forecastEndUtc &&
                   (endDateUtc is null ||
                    occurrence <= endDateUtc.Value))
            {
                AddEvent(
                    currency,
                    occurrence,
                    schedule.Type,
                    schedule.Amount);

                occurrence = RecurrenceCalculator.GetNextOccurrence(
                    startDateUtc,
                    schedule.Frequency,
                    occurrence,
                    schedule.TimeZoneId);
            }
        }

        var currencyForecasts = startingBalances
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(currency =>
            {
                var projectedBalance = currency.Value;
                var days = new List<ForecastDayResponse>(
                    horizonDays);

                for (var offset = 0; offset < horizonDays; offset++)
                {
                    var date = localToday.AddDays(offset);
                    events.TryGetValue(
                        (currency.Key, date),
                        out var dayEvent);

                    var income = dayEvent?.Income ?? 0;
                    var expenses = dayEvent?.Expenses ?? 0;
                    projectedBalance += income - expenses;
                    days.Add(new ForecastDayResponse(
                        date,
                        income,
                        expenses,
                        income - expenses,
                        projectedBalance,
                        dayEvent?.PlannedItems ?? 0));
                }

                return new ForecastCurrencyResponse(
                    currency.Key,
                    currency.Value,
                    projectedBalance,
                    days);
            })
            .ToList();

        return new CashFlowForecastResponse(
            horizonDays,
            nowUtc,
            currencyForecasts);
    }

    private async Task<TimeZoneInfo> GetUserTimeZoneAsync(
        Guid userId,
        CancellationToken ct)
    {
        var timeZoneId = await _db.UserPreferences
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.TimeZone)
            .FirstOrDefaultAsync(ct) ?? DefaultTimeZoneId;

        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    private static DateTime ToUtc(
        DateTime localDate,
        TimeZoneInfo timeZone)
    {
        var unspecified = DateTime.SpecifyKind(
            localDate,
            DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(
            unspecified,
            timeZone);
    }

    private sealed class ForecastEvent
    {
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public int PlannedItems { get; set; }
    }
}
