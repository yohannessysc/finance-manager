
using FinanceManager.Application.DTOs.Dashboard;
using FinanceManager.Application.DTOs.Transactions;
using FinanceManager.Application.Interfaces;
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
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(
            DefaultTimeZoneId);

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
        var accountCurrencies = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => new
            {
                a.Id,
                a.Currency
            })
            .ToDictionaryAsync(
                a => a.Id,
                a => a.Currency,
                ct);

        // 2. Transactions for the last six calendar months.
        var transactions = await _db.FinancialTransactions
            .AsNoTracking()
            .Where(t =>
                t.UserId == userId &&
                t.TransactionDateUtc >= rangeStartUtc &&
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

        // 4. Current-month spending grouped by category.
        var categories = await _db.TransactionCategories
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        var currentExpenses = transactions
            .Where(t =>
                t.TransactionDateUtc >= currentMonthStartUtc &&
                t.TransactionDateUtc < rangeEndUtc &&
                t.Type == TransactionType.Expense)
            .Where(t =>
                accountCurrencies.ContainsKey(
                    t.FinancialAccountId))
            .ToList();

        var groupedExpenses = currentExpenses
            .GroupBy(t => new
            {
                Currency = accountCurrencies[t.FinancialAccountId],
                t.CategoryId
            })
            .Select(g => new
            {
                g.Key.Currency,
                g.Key.CategoryId,
                Amount = g.Sum(t => t.Amount)
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
            .Where(t => t.UserId == userId)
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
                r.UserId == userId &&
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
}
